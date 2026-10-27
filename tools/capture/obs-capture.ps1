<#
    Read-only capture of what a running OBS Studio answers, as raw JSON, so that the nested objects can be compared with what the
    client models. It only sends Get requests (no stream key: GetStreamServiceSettings is not asked, no screenshots), and then
    listens for events for a while: change something in OBS Studio meanwhile (mute an input, switch a scene) to capture them.
    Nothing is changed in OBS Studio.

    pwsh tools/capture/obs-capture.ps1 [-Password <password>] [-Port 4455] [-EventSeconds 30] [-OutDir <folder>]

    The password can also be in the environment variable OBS_WEBSOCKET_PASSWORD. The script writes one file per request to the
    folder (the status and the response data exactly as OBS Studio sent them), the events to events.jsonl, and a summary to
    capture-summary.txt. The folder holds what identifies your machine and your scenes: it is not for the repository (the
    fixtures of the tests are made from it by make-fixture.py, which replaces what identifies the machine).
#>
param(
    [string]$Server = 'localhost',
    [int]$Port = 4455,
    [string]$Password = $env:OBS_WEBSOCKET_PASSWORD,
    [int]$EventSeconds = 30,
    [string]$OutDir = (Join-Path (Get-Location) 'obs-capture')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$none = [System.Threading.CancellationToken]::None
$null = $ws.ConnectAsync([Uri]"ws://${Server}:${Port}", $none).GetAwaiter().GetResult()
$sha = [System.Security.Cryptography.SHA256]::Create()
$events = [System.Collections.Generic.List[string]]::new()
$summary = [System.Collections.Generic.List[string]]::new()

function Get-Sha256Base64([string]$text) {
    [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))
}

function Receive-Message([System.Threading.CancellationToken]$Token = [System.Threading.CancellationToken]::None) {
    $buffer = New-Object byte[] 65536
    $segment = [ArraySegment[byte]]::new($buffer)
    $stream = New-Object System.IO.MemoryStream
    do {
        $result = $ws.ReceiveAsync($segment, $Token).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { throw 'OBS Studio closed the connection.' }
        $stream.Write($buffer, 0, $result.Count)
    } while (-not $result.EndOfMessage)
    [Text.Encoding]::UTF8.GetString($stream.ToArray())
}

function Send-Message($message) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($message | ConvertTo-Json -Depth 20 -Compress))
    $null = $ws.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $none).GetAwaiter().GetResult()
}

# The handshake. obs-websocket 5: the answer to the challenge is the hash of (the hash of password + salt) + challenge.
$hello = (Receive-Message | ConvertFrom-Json)
$identify = @{ rpcVersion = 1; eventSubscriptions = 4095 }
if ($hello.d.authentication) {
    if (-not $Password) { throw 'OBS Studio asks for a password: run the script with -Password.' }
    $secret = Get-Sha256Base64 ($Password + $hello.d.authentication.salt)
    $identify.authentication = Get-Sha256Base64 ($secret + $hello.d.authentication.challenge)
}
Send-Message @{ op = 1; d = $identify }
$identified = (Receive-Message | ConvertFrom-Json)
if ($identified.op -ne 2) { throw "The handshake failed: $($identified | ConvertTo-Json -Compress)" }
$summary.Add("Connected to obs-websocket $($hello.d.obsWebSocketVersion) (rpc $($identified.d.negotiatedRpcVersion))")

function Invoke-Request([string]$Type, $Data = $null, [string]$Label = $null) {
    $id = [guid]::NewGuid().ToString()
    $d = @{ requestType = $Type; requestId = $id }
    if ($null -ne $Data) { $d.requestData = $Data }
    Send-Message @{ op = 6; d = $d }
    $timeout = [System.Threading.CancellationTokenSource]::new(15000)
    try {
        while ($true) {
            $message = Receive-Message $timeout.Token
            $parsed = $message | ConvertFrom-Json
            if ($parsed.op -eq 5) { $events.Add($message); continue }
            if ($parsed.op -eq 7 -and $parsed.d.requestId -eq $id) { break }
        }
    }
    finally { $timeout.Dispose() }

    $name = if ($Label) { "${Type}__$Label" } else { $Type }
    $name = ($name -replace '[^A-Za-z0-9_.-]', '_')
    # The message is written as OBS Studio sent it, not re-serialized.
    Set-Content -LiteralPath (Join-Path $OutDir "$name.json") -Value $message -Encoding UTF8
    $status = "$($parsed.d.requestStatus.code)"
    $summary.Add("$name -> $status" + $(if (-not $parsed.d.requestStatus.result) { " ($($parsed.d.requestStatus.comment))" } else { '' }))
    $parsed.d
}

function Get-Field($response, [string]$name) {
    if ($response.requestStatus.result -and $response.responseData) { $response.responseData.$name } else { @() }
}

# Requests without parameters. GetStreamServiceSettings is left out on purpose: it holds the stream key.
foreach ($type in 'GetVersion', 'GetStats', 'GetHotkeyList', 'GetSpecialInputs', 'GetGroupList', 'GetCanvasList',
    'GetProfileList', 'GetSceneCollectionList', 'GetVideoSettings', 'GetStreamStatus', 'GetRecordStatus', 'GetRecordDirectory',
    'GetVirtualCamStatus', 'GetReplayBufferStatus', 'GetOutputList', 'GetTransitionKindList', 'GetSceneTransitionList',
    'GetCurrentSceneTransition', 'GetCurrentSceneTransitionCursor', 'GetStudioModeEnabled', 'GetMonitorList',
    'GetCurrentProgramScene', 'GetCurrentPreviewScene') {
    $null = Invoke-Request $type
}

$scenes = Invoke-Request 'GetSceneList'
$inputs = Invoke-Request 'GetInputList'
$kinds = Invoke-Request 'GetInputKindList' @{ unversioned = $false }
$null = Invoke-Request 'GetInputKindList' @{ unversioned = $true } 'unversioned'

foreach ($kind in (Get-Field $kinds 'inputKinds')) {
    $null = Invoke-Request 'GetInputDefaultSettings' @{ inputKind = $kind } $kind
}

foreach ($scene in (Get-Field $scenes 'scenes')) {
    $sceneUuid = $scene.sceneUuid
    $label = $scene.sceneName
    $null = Invoke-Request 'GetSceneSceneTransitionOverride' @{ sceneUuid = $sceneUuid } $label
    $items = Invoke-Request 'GetSceneItemList' @{ sceneUuid = $sceneUuid } $label
    foreach ($item in (Get-Field $items 'sceneItems')) {
        $itemData = @{ sceneUuid = $sceneUuid; sceneItemId = $item.sceneItemId }
        $itemLabel = "$label-$($item.sceneItemId)"
        foreach ($type in 'GetSceneItemTransform', 'GetSceneItemEnabled', 'GetSceneItemLocked', 'GetSceneItemBlendMode', 'GetSceneItemIndex', 'GetSceneItemSource') {
            $null = Invoke-Request $type $itemData $itemLabel
        }
    }
}

foreach ($group in (Get-Field (Invoke-Request 'GetGroupList') 'groups')) {
    $null = Invoke-Request 'GetGroupSceneItemList' @{ sceneName = $group } $group
}

foreach ($input in (Get-Field $inputs 'inputs')) {
    $inputData = @{ inputUuid = $input.inputUuid }
    $label = $input.inputName
    foreach ($type in 'GetInputSettings', 'GetInputMute', 'GetInputVolume', 'GetInputAudioBalance', 'GetInputAudioSyncOffset',
        'GetInputAudioMonitorType', 'GetInputAudioTracks', 'GetInputDeinterlaceMode', 'GetInputDeinterlaceFieldOrder') {
        $null = Invoke-Request $type $inputData $label
    }
    $null = Invoke-Request 'GetSourceActive' @{ sourceUuid = $input.inputUuid } $label
    if ($input.inputKind -in 'ffmpeg_source', 'vlc_source') {
        $null = Invoke-Request 'GetMediaInputStatus' $inputData $label
    }

    $filters = Invoke-Request 'GetSourceFilterList' @{ sourceUuid = $input.inputUuid } $label
    foreach ($filter in (Get-Field $filters 'filters')) {
        $null = Invoke-Request 'GetSourceFilter' @{ sourceUuid = $input.inputUuid; filterName = $filter.filterName } "$label-$($filter.filterName)"
    }
}

# Events: change something in OBS Studio now. Cancelling a receive ends the connection, so the listening has one deadline.
$summary.Add("Listening for events for $EventSeconds seconds")
$listening = [System.Threading.CancellationTokenSource]::new($EventSeconds * 1000)
try {
    while ($true) {
        $message = Receive-Message $listening.Token
        if (($message | ConvertFrom-Json).op -eq 5) { $events.Add($message) }
    }
}
catch [System.OperationCanceledException] { }
catch [System.Net.WebSockets.WebSocketException] { }

Set-Content -LiteralPath (Join-Path $OutDir 'events.jsonl') -Value $events -Encoding UTF8
$summary.Add("$($events.Count) events captured")
Set-Content -LiteralPath (Join-Path $OutDir 'capture-summary.txt') -Value $summary -Encoding UTF8
try { $null = $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done', $none).GetAwaiter().GetResult() } catch { }
$summary | Select-Object -Last 8
"Written to $OutDir"
