<#
    Capture of what a running OBS Studio answers, as raw JSON, so that the nested objects can be compared with what the client
    models. By default it is read-only: it only sends Get requests (no stream key: GetStreamServiceSettings is not asked, no
    screenshots), and then listens for events for a while: change something in OBS Studio meanwhile (mute an input, switch a
    scene) to capture them. Nothing is changed in OBS Studio.

    pwsh tools/capture/obs-capture.ps1 [-Password <password>] [-Port 4455] [-EventSeconds 30] [-OutDir <folder>] [-WithSampleContent]

    With -WithSampleContent the script first creates a scene of its own (obs-capture-sample) with a few sources, filters and a
    transform, so that there are scene items, filters and a media source to capture, changes some of it (which causes events
    that are captured as well), and removes all of it again at the end, also when something fails. It only touches what it
    created itself, and it does not switch scenes, so nothing reaches a stream or a recording. Without a scene collection of
    your own to spare, leave it out.

    The events are subscribed to all, the high-volume ones included (the volume meters alone send dozens a second): the first
    -MaxEventsPerType events of each type are kept.

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
    [int]$MaxEventsPerType = 5,
    [string]$OutDir = (Join-Path (Get-Location) 'obs-capture'),
    [switch]$WithSampleContent
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$none = [System.Threading.CancellationToken]::None
$sha = [System.Security.Cryptography.SHA256]::Create()
$events = [System.Collections.Generic.List[string]]::new()
$eventCounts = @{}
$summary = [System.Collections.Generic.List[string]]::new()
# All (4095, with the canvases) and the four high-volume events: the volume meters, the active state, the show state and the transforms.
$subscriptions = 4095 -bor (1 -shl 16) -bor (1 -shl 17) -bor (1 -shl 18) -bor (1 -shl 19)

function Get-Sha256Base64([string]$text) {
    [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))
}

function Receive-Message([System.Threading.CancellationToken]$Token = [System.Threading.CancellationToken]::None) {
    $buffer = New-Object byte[] 65536
    $segment = [ArraySegment[byte]]::new($buffer)
    $stream = New-Object System.IO.MemoryStream
    do {
        $result = $script:ws.ReceiveAsync($segment, $Token).GetAwaiter().GetResult()
        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { throw 'OBS Studio closed the connection.' }
        $stream.Write($buffer, 0, $result.Count)
    } while (-not $result.EndOfMessage)
    [Text.Encoding]::UTF8.GetString($stream.ToArray())
}

function Send-Message($message) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($message | ConvertTo-Json -Depth 20 -Compress))
    $null = $script:ws.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $none).GetAwaiter().GetResult()
}

# The handshake. obs-websocket 5: the answer to the challenge is the hash of (the hash of password + salt) + challenge.
function Connect-Obs {
    $script:ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $null = $script:ws.ConnectAsync([Uri]"ws://${Server}:${Port}", $none).GetAwaiter().GetResult()
    $hello = (Receive-Message | ConvertFrom-Json)
    $identify = @{ rpcVersion = 1; eventSubscriptions = $subscriptions }
    if ($hello.d.authentication) {
        if (-not $Password) { throw 'OBS Studio asks for a password: run the script with -Password.' }
        $secret = Get-Sha256Base64 ($Password + $hello.d.authentication.salt)
        $identify.authentication = Get-Sha256Base64 ($secret + $hello.d.authentication.challenge)
    }
    Send-Message @{ op = 1; d = $identify }
    $identified = (Receive-Message | ConvertFrom-Json)
    if ($identified.op -ne 2) { throw "The handshake failed: $($identified | ConvertTo-Json -Compress)" }
    $summary.Add("Connected to obs-websocket $($hello.d.obsWebSocketVersion) (rpc $($identified.d.negotiatedRpcVersion))")
}

Connect-Obs

# The events are counted per type, and only the first few of each type are kept.
function Add-Event([string]$message) {
    $type = ($message | ConvertFrom-Json).d.eventType
    $count = 1 + [int]$eventCounts[$type]
    $eventCounts[$type] = $count
    if ($count -le $MaxEventsPerType) { $events.Add($message) }
}

function Invoke-Request([string]$Type, $Data = $null, [string]$Label = $null, [switch]$Setup, [int]$TimeoutSeconds = 15) {
    $id = [guid]::NewGuid().ToString()
    $d = @{ requestType = $Type; requestId = $id }
    if ($null -ne $Data) { $d.requestData = $Data }
    Send-Message @{ op = 6; d = $d }
    $timeout = [System.Threading.CancellationTokenSource]::new($TimeoutSeconds * 1000)
    try {
        while ($true) {
            $message = Receive-Message $timeout.Token
            $parsed = $message | ConvertFrom-Json
            if ($parsed.op -eq 5) { Add-Event $message; continue }
            if ($parsed.op -eq 7 -and $parsed.d.requestId -eq $id) { break }
        }
    }
    finally { $timeout.Dispose() }

    $comment = if (-not $parsed.d.requestStatus.result) { " ($($parsed.d.requestStatus.comment))" } else { '' }
    if ($Setup) {
        # Requests that change OBS Studio (the sample content) are not part of the capture.
        $summary.Add("setup: $Type -> $($parsed.d.requestStatus.code)$comment")
        return $parsed.d
    }

    $name = if ($Label) { "${Type}__$Label" } else { $Type }
    $name = ($name -replace '[^A-Za-z0-9_.-]', '_')
    # The message is written as OBS Studio sent it, not re-serialized.
    Set-Content -LiteralPath (Join-Path $OutDir "$name.json") -Value $message -Encoding UTF8
    $summary.Add("$name -> $($parsed.d.requestStatus.code)$comment")
    $parsed.d
}

function Get-Field($response, [string]$name) {
    if ($response.requestStatus.result -and $response.responseData) { $response.responseData.$name } else { @() }
}

# What the sample content created, to remove again.
$sample = @{ SceneUuid = $null; InputUuids = [System.Collections.Generic.List[string]]::new(); Items = @{} }

function New-SampleContent {
    $created = Invoke-Request 'CreateScene' @{ sceneName = 'obs-capture-sample' } -Setup
    if (-not $created.requestStatus.result) { throw "The sample scene could not be created: $($created.requestStatus.comment)" }
    $sample.SceneUuid = $created.responseData.sceneUuid
    $scene = $sample.SceneUuid

    $inputKinds = @(Get-Field (Invoke-Request 'GetInputKindList' @{ unversioned = $false } -Setup) 'inputKinds')
    $filterKinds = @(Get-Field (Invoke-Request 'GetSourceFilterKindList' -Setup) 'sourceFilterKinds')
    $textKind = @('text_gdiplus_v3', 'text_ft2_source_v2') | Where-Object { $inputKinds -contains $_ } | Select-Object -First 1

    $inputs = @(
        @{ name = 'obs-capture-color'; kind = 'color_source_v3'; settings = @{ color = 4278190335; width = 640; height = 360 } },
        @{ name = 'obs-capture-media'; kind = 'ffmpeg_source'; settings = @{ is_local_file = $true; local_file = '' } },
        @{ name = 'obs-capture-image'; kind = 'image_source'; settings = @{ file = '' } }
    )
    if ($textKind) {
        $inputs += @{ name = 'obs-capture-text'; kind = $textKind; settings = @{ text = 'obs-capture'; font = @{ face = 'Arial'; size = 48; style = 'Regular'; flags = 0 } } }
    }

    $items = $sample.Items
    foreach ($input in $inputs) {
        if ($inputKinds -notcontains $input.kind) { continue }
        $result = Invoke-Request 'CreateInput' @{ sceneUuid = $scene; inputName = $input.name; inputKind = $input.kind; inputSettings = $input.settings; sceneItemEnabled = $true } -Setup
        if ($result.requestStatus.result) {
            $sample.InputUuids.Add($result.responseData.inputUuid)
            $items[$input.name] = $result.responseData
        }
    }

    if ($items['obs-capture-color']) {
        $color = $items['obs-capture-color']
        $transform = @{
            positionX = 100.5; positionY = 50.25; rotation = 15.0; scaleX = 1.5; scaleY = 1.25; alignment = 5
            cropLeft = 10; cropRight = 5; cropTop = 3; cropBottom = 2; cropToBounds = $true
            boundsType = 'OBS_BOUNDS_STRETCH'; boundsAlignment = 0; boundsWidth = 300.0; boundsHeight = 200.0
        }
        $null = Invoke-Request 'SetSceneItemTransform' @{ sceneUuid = $scene; sceneItemId = $color.sceneItemId; sceneItemTransform = $transform } -Setup
        $null = Invoke-Request 'SetSceneItemBlendMode' @{ sceneUuid = $scene; sceneItemId = $color.sceneItemId; sceneItemBlendMode = 'OBS_BLEND_MULTIPLY' } -Setup
        $filters = @(
            @{ name = 'obs-capture-color-correction'; kind = 'color_filter_v2'; settings = @{ brightness = 0.25; gamma = 0.1 } },
            @{ name = 'obs-capture-chroma-key'; kind = 'chroma_key_filter_v2'; settings = @{ key_color_type = 'green' } },
            @{ name = 'obs-capture-crop'; kind = 'crop_filter'; settings = @{ left = 5; right = 5; top = 5; bottom = 5; relative = $true } }
        )
        foreach ($filter in $filters) {
            if ($filterKinds -notcontains $filter.kind) { continue }
            $null = Invoke-Request 'CreateSourceFilter' @{ sourceUuid = $color.inputUuid; filterName = $filter.name; filterKind = $filter.kind; filterSettings = $filter.settings } -Setup
        }
    }

    if ($items['obs-capture-text']) {
        $null = Invoke-Request 'SetSceneItemLocked' @{ sceneUuid = $scene; sceneItemId = $items['obs-capture-text'].sceneItemId; sceneItemLocked = $true } -Setup
    }

    $null = Invoke-Request 'SetSceneSceneTransitionOverride' @{ sceneUuid = $scene; transitionName = 'Fade'; transitionDuration = 500 } -Setup
}

# Changes, to what the script created itself only: for the events that they cause, and for answers that are not the defaults.
function Invoke-SampleChanges {
    $scene = $sample.SceneUuid
    $color = $sample.Items['obs-capture-color']
    $media = $sample.Items['obs-capture-media']
    $text = $sample.Items['obs-capture-text']

    if ($media) {
        $mediaInput = @{ inputUuid = $media.inputUuid }
        $null = Invoke-Request 'SetInputMute' ($mediaInput + @{ inputMuted = $true }) -Setup
        $null = Invoke-Request 'SetInputVolume' ($mediaInput + @{ inputVolumeDb = -12.5 }) -Setup
        $null = Invoke-Request 'SetInputAudioBalance' ($mediaInput + @{ inputAudioBalance = 0.25 }) -Setup
        $null = Invoke-Request 'SetInputAudioSyncOffset' ($mediaInput + @{ inputAudioSyncOffset = 150 }) -Setup
        $null = Invoke-Request 'SetInputAudioTracks' ($mediaInput + @{ inputAudioTracks = @{ '1' = $true; '2' = $false } }) -Setup
        $null = Invoke-Request 'TriggerMediaInputAction' ($mediaInput + @{ mediaAction = 'OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE' }) -Setup
        $null = Invoke-Request 'CreateSceneItem' @{ sceneUuid = $scene; sourceUuid = $media.inputUuid } -Setup
    }

    if ($color) {
        $item = @{ sceneUuid = $scene; sceneItemId = $color.sceneItemId }
        $null = Invoke-Request 'SetInputSettings' @{ inputUuid = $color.inputUuid; inputSettings = @{ color = 4294901760 }; overlay = $true } -Setup
        $null = Invoke-Request 'SetSceneItemEnabled' ($item + @{ sceneItemEnabled = $false }) -Setup
        $null = Invoke-Request 'SetSceneItemEnabled' ($item + @{ sceneItemEnabled = $true }) -Setup
        $null = Invoke-Request 'SetSceneItemIndex' ($item + @{ sceneItemIndex = 2 }) -Setup
        $null = Invoke-Request 'SetSceneItemTransform' ($item + @{ sceneItemTransform = @{ positionX = 200.0; rotation = 30.5 } }) -Setup
        $copy = Invoke-Request 'DuplicateSceneItem' $item -Setup
        if ($copy.requestStatus.result) {
            $null = Invoke-Request 'RemoveSceneItem' @{ sceneUuid = $scene; sceneItemId = $copy.responseData.sceneItemId } -Setup
        }

        $filterSource = @{ sourceUuid = $color.inputUuid }
        $null = Invoke-Request 'SetSourceFilterEnabled' ($filterSource + @{ filterName = 'obs-capture-crop'; filterEnabled = $false }) -Setup
        $null = Invoke-Request 'SetSourceFilterIndex' ($filterSource + @{ filterName = 'obs-capture-crop'; filterIndex = 0 }) -Setup
        $null = Invoke-Request 'SetSourceFilterSettings' ($filterSource + @{ filterName = 'obs-capture-chroma-key'; filterSettings = @{ similarity = 300 }; overlay = $true }) -Setup
        $null = Invoke-Request 'SetSourceFilterName' ($filterSource + @{ filterName = 'obs-capture-crop'; newFilterName = 'obs-capture-crop-renamed' }) -Setup
    }

    if ($text) {
        $null = Invoke-Request 'SetInputName' @{ inputUuid = $text.inputUuid; newInputName = 'obs-capture-text-renamed' } -Setup
    }

    $null = Invoke-Request 'BroadcastCustomEvent' @{ eventData = @{ capture = 'obs-capture'; numbers = @(1, 2.5); nested = @{ ok = $true } } } -Setup
    $null = Invoke-Request 'SetSceneName' @{ sceneUuid = $scene; newSceneName = 'obs-capture-sample-renamed' } -Setup
}

# Removes what the sample content created. When a request had no answer, the connection is gone: connect again for it.
function Remove-SampleContent {
    if (-not $sample.SceneUuid -and $sample.InputUuids.Count -eq 0) { return }
    $left = 'the scene obs-capture-sample (or obs-capture-sample-renamed) and the inputs obs-capture-*'
    try {
        if ($script:ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { Connect-Obs }
        foreach ($uuid in $sample.InputUuids) {
            $null = Invoke-Request 'RemoveInput' @{ inputUuid = $uuid } -Setup -TimeoutSeconds 30
        }
        if ($sample.SceneUuid) {
            $null = Invoke-Request 'RemoveScene' @{ sceneUuid = $sample.SceneUuid } -Setup -TimeoutSeconds 30
        }
    }
    catch {
        Write-Warning "OBS Studio did not answer, so the sample content could not be removed: remove $left by hand."
        $summary.Add("setup: the sample content could not be removed ($($_.Exception.Message))")
    }
}

try {
    if ($WithSampleContent) {
        New-SampleContent
        Invoke-SampleChanges
    }

    # Requests without parameters. GetStreamServiceSettings is left out on purpose: it holds the stream key.
    foreach ($type in 'GetVersion', 'GetStats', 'GetHotkeyList', 'GetSpecialInputs', 'GetGroupList', 'GetCanvasList',
        'GetProfileList', 'GetSceneCollectionList', 'GetVideoSettings', 'GetStreamStatus', 'GetRecordStatus', 'GetRecordDirectory',
        'GetVirtualCamStatus', 'GetReplayBufferStatus', 'GetTransitionKindList', 'GetSceneTransitionList',
        'GetCurrentSceneTransition', 'GetCurrentSceneTransitionCursor', 'GetStudioModeEnabled', 'GetMonitorList',
        'GetCurrentProgramScene', 'GetCurrentPreviewScene') {
        $null = Invoke-Request $type
    }

    foreach ($output in (Get-Field (Invoke-Request 'GetOutputList') 'outputs')) {
        $null = Invoke-Request 'GetOutputStatus' @{ outputName = $output.outputName } $output.outputName
    }

    # A parameter that does not exist is answered with null values (the last one).
    foreach ($parameter in @('Output', 'Mode'), @('Video', 'BaseCX'), @('Video', 'ObsCaptureNoSuchParameter')) {
        $null = Invoke-Request 'GetProfileParameter' @{ parameterCategory = $parameter[0]; parameterName = $parameter[1] } "$($parameter[0])-$($parameter[1])"
    }

    $scenes = Invoke-Request 'GetSceneList'
    $inputs = Invoke-Request 'GetInputList'
    $kinds = Invoke-Request 'GetInputKindList' @{ unversioned = $false }
    $null = Invoke-Request 'GetInputKindList' @{ unversioned = $true } 'unversioned'
    $filterKinds = Invoke-Request 'GetSourceFilterKindList'

    foreach ($kind in (Get-Field $kinds 'inputKinds')) {
        $null = Invoke-Request 'GetInputDefaultSettings' @{ inputKind = $kind } $kind
    }

    foreach ($kind in (Get-Field $filterKinds 'sourceFilterKinds')) {
        $null = Invoke-Request 'GetSourceFilterDefaultSettings' @{ filterKind = $kind } $kind
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
            $null = Invoke-Request 'GetSceneItemId' @{ sceneUuid = $sceneUuid; sourceName = $item.sourceName } $itemLabel
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

        # The items of a list property (the alignment of a text), not the devices of the machine.
        if ($input.inputKind -eq 'text_gdiplus_v3') {
            $null = Invoke-Request 'GetInputPropertiesListPropertyItems' @{ inputUuid = $input.inputUuid; propertyName = 'align' } $label
        }

        $filters = Invoke-Request 'GetSourceFilterList' @{ sourceUuid = $input.inputUuid } $label
        foreach ($filter in (Get-Field $filters 'filters')) {
            $null = Invoke-Request 'GetSourceFilter' @{ sourceUuid = $input.inputUuid; filterName = $filter.filterName } "$label-$($filter.filterName)"
        }
    }
}
finally {
    if ($WithSampleContent) { Remove-SampleContent }
}

# Events: change something in OBS Studio now. Cancelling a receive ends the connection, so the listening has one deadline.
$summary.Add("Listening for events for $EventSeconds seconds")
$listening = [System.Threading.CancellationTokenSource]::new($EventSeconds * 1000)
try {
    while ($true) {
        $message = Receive-Message $listening.Token
        if (($message | ConvertFrom-Json).op -eq 5) { Add-Event $message }
    }
}
catch [System.OperationCanceledException] { }
catch [System.Net.WebSockets.WebSocketException] { }

Set-Content -LiteralPath (Join-Path $OutDir 'events.jsonl') -Value $events -Encoding UTF8
$summary.Add("$($events.Count) events kept of $(($eventCounts.Values | Measure-Object -Sum).Sum) received, in $($eventCounts.Count) types")
foreach ($type in ($eventCounts.Keys | Sort-Object)) { $summary.Add("event ${type}: $($eventCounts[$type]) received") }
Set-Content -LiteralPath (Join-Path $OutDir 'capture-summary.txt') -Value $summary -Encoding UTF8
try { $null = $script:ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done', $none).GetAwaiter().GetResult() } catch { }
$summary | Select-Object -Last ($eventCounts.Count + 2)
"Written to $OutDir"
