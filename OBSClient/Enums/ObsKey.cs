namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Keys.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObsKey
    {
        /// <summary>
        /// None
        /// </summary>
        OBS_KEY_NONE,

        /// <summary>
        /// Return
        /// </summary>
        OBS_KEY_RETURN,

        /// <summary>
        /// Enter
        /// </summary>
        OBS_KEY_ENTER,

        /// <summary>
        /// Escape
        /// </summary>
        OBS_KEY_ESCAPE,

        /// <summary>
        /// Tab
        /// </summary>
        OBS_KEY_TAB,

        /// <summary>
        /// Backtab
        /// </summary>
        OBS_KEY_BACKTAB,

        /// <summary>
        /// Backspace
        /// </summary>
        OBS_KEY_BACKSPACE,

        /// <summary>
        /// Insert
        /// </summary>
        OBS_KEY_INSERT,

        /// <summary>
        /// Delete
        /// </summary>
        OBS_KEY_DELETE,

        /// <summary>
        /// Pause
        /// </summary>
        OBS_KEY_PAUSE,

        /// <summary>
        /// Print
        /// </summary>
        OBS_KEY_PRINT,

        /// <summary>
        /// Sysreq
        /// </summary>
        OBS_KEY_SYSREQ,

        /// <summary>
        /// Clear
        /// </summary>
        OBS_KEY_CLEAR,

        /// <summary>
        /// Home
        /// </summary>
        OBS_KEY_HOME,

        /// <summary>
        /// End
        /// </summary>
        OBS_KEY_END,

        /// <summary>
        /// Left
        /// </summary>
        OBS_KEY_LEFT,

        /// <summary>
        /// Up
        /// </summary>
        OBS_KEY_UP,

        /// <summary>
        /// Right
        /// </summary>
        OBS_KEY_RIGHT,

        /// <summary>
        /// Down
        /// </summary>
        OBS_KEY_DOWN,

        /// <summary>
        /// PageUp
        /// </summary>
        OBS_KEY_PAGEUP,

        /// <summary>
        /// PageDown
        /// </summary>
        OBS_KEY_PAGEDOWN,

        /// <summary>
        /// Shift
        /// </summary>
        OBS_KEY_SHIFT,

        /// <summary>
        /// Control
        /// </summary>
        OBS_KEY_CONTROL,

        /// <summary>
        /// Meta
        /// </summary>
        OBS_KEY_META,

        /// <summary>
        /// Alt
        /// </summary>
        OBS_KEY_ALT,

        /// <summary>
        /// AltGr
        /// </summary>
        OBS_KEY_ALTGR,

        /// <summary>
        /// CapsLock
        /// </summary>
        OBS_KEY_CAPSLOCK,

        /// <summary>
        /// NumLock
        /// </summary>
        OBS_KEY_NUMLOCK,

        /// <summary>
        /// ScrolLock
        /// </summary>
        OBS_KEY_SCROLLLOCK,

        /// <summary>
        /// F1
        /// </summary>
        OBS_KEY_F1,

        /// <summary>
        /// F2
        /// </summary>
        OBS_KEY_F2,

        /// <summary>
        /// F3
        /// </summary>
        OBS_KEY_F3,

        /// <summary>
        /// F4
        /// </summary>
        OBS_KEY_F4,

        /// <summary>
        /// F5
        /// </summary>
        OBS_KEY_F5,

        /// <summary>
        /// F6
        /// </summary>
        OBS_KEY_F6,

        /// <summary>
        /// F7
        /// </summary>
        OBS_KEY_F7,

        /// <summary>
        /// F8
        /// </summary>
        OBS_KEY_F8,

        /// <summary>
        /// F9
        /// </summary>
        OBS_KEY_F9,

        /// <summary>
        /// F10
        /// </summary>
        OBS_KEY_F10,

        /// <summary>
        /// F11
        /// </summary>
        OBS_KEY_F11,

        /// <summary>
        /// F12
        /// </summary>
        OBS_KEY_F12,

        /// <summary>
        /// F13
        /// </summary>
        OBS_KEY_F13,

        /// <summary>
        /// F14
        /// </summary>
        OBS_KEY_F14,

        /// <summary>
        /// F15
        /// </summary>
        OBS_KEY_F15,

        /// <summary>
        /// F16
        /// </summary>
        OBS_KEY_F16,

        /// <summary>
        /// F17
        /// </summary>
        OBS_KEY_F17,

        /// <summary>
        /// F18
        /// </summary>
        OBS_KEY_F18,

        /// <summary>
        /// F19
        /// </summary>
        OBS_KEY_F19,

        /// <summary>
        /// F20
        /// </summary>
        OBS_KEY_F20,

        /// <summary>
        /// F21
        /// </summary>
        OBS_KEY_F21,

        /// <summary>
        /// F22
        /// </summary>
        OBS_KEY_F22,

        /// <summary>
        /// F23
        /// </summary>
        OBS_KEY_F23,

        /// <summary>
        /// F24
        /// </summary>
        OBS_KEY_F24,

        /// <summary>
        /// F25
        /// </summary>
        OBS_KEY_F25,

        /// <summary>
        /// F26
        /// </summary>
        OBS_KEY_F26,

        /// <summary>
        /// F27
        /// </summary>
        OBS_KEY_F27,

        /// <summary>
        /// F28
        /// </summary>
        OBS_KEY_F28,

        /// <summary>
        /// F29
        /// </summary>
        OBS_KEY_F29,

        /// <summary>
        /// F30
        /// </summary>
        OBS_KEY_F30,

        /// <summary>
        /// F31
        /// </summary>
        OBS_KEY_F31,

        /// <summary>
        /// F32
        /// </summary>
        OBS_KEY_F32,

        /// <summary>
        /// F33
        /// </summary>
        OBS_KEY_F33,

        /// <summary>
        /// F34
        /// </summary>
        OBS_KEY_F34,

        /// <summary>
        /// F35
        /// </summary>
        OBS_KEY_F35,

        /// <summary>
        /// Menu
        /// </summary>
        OBS_KEY_MENU,

        /// <summary>
        /// Hyper-L
        /// </summary>
        OBS_KEY_HYPER_L,

        /// <summary>
        /// Hyper-R
        /// </summary>
        OBS_KEY_HYPER_R,

        /// <summary>
        /// Help
        /// </summary>
        OBS_KEY_HELP,

        /// <summary>
        /// Direction Left
        /// </summary>
        OBS_KEY_DIRECTION_L,

        /// <summary>
        /// Direction Right
        /// </summary>
        OBS_KEY_DIRECTION_R,

        /// <summary>
        /// Space
        /// </summary>
        OBS_KEY_SPACE,

        /// <summary>
        /// !
        /// </summary>
        OBS_KEY_EXCLAM,

        /// <summary>
        /// "
        /// </summary>
        OBS_KEY_QUOTEDBL,

        /// <summary>
        /// Numbersign
        /// </summary>
        OBS_KEY_NUMBERSIGN,

        /// <summary>
        /// $
        /// </summary>
        OBS_KEY_DOLLAR,

        /// <summary>
        /// %
        /// </summary>
        OBS_KEY_PERCENT,

        /// <summary>
        /// Ampersand
        /// </summary>
        OBS_KEY_AMPERSAND,

        /// <summary>
        /// `
        /// </summary>
        OBS_KEY_APOSTROPHE,

        /// <summary>
        /// (
        /// </summary>
        OBS_KEY_PARENLEFT,

        /// <summary>
        /// )
        /// </summary>
        OBS_KEY_PARENRIGHT,

        /// <summary>
        /// *
        /// </summary>
        OBS_KEY_ASTERISK,

        /// <summary>
        /// +
        /// </summary>
        OBS_KEY_PLUS,

        /// <summary>
        /// ,
        /// </summary>
        OBS_KEY_COMMA,

        /// <summary>
        /// -
        /// </summary>
        OBS_KEY_MINUS,

        /// <summary>
        /// .
        /// </summary>
        OBS_KEY_PERIOD,

        /// <summary>
        /// /
        /// </summary>
        OBS_KEY_SLASH,

        /// <summary>
        /// 0
        /// </summary>
        OBS_KEY_0,

        /// <summary>
        /// 1
        /// </summary>
        OBS_KEY_1,

        /// <summary>
        /// 2
        /// </summary>
        OBS_KEY_2,

        /// <summary>
        /// 3
        /// </summary>
        OBS_KEY_3,

        /// <summary>
        /// 4
        /// </summary>
        OBS_KEY_4,

        /// <summary>
        /// 5
        /// </summary>
        OBS_KEY_5,

        /// <summary>
        /// 6
        /// </summary>
        OBS_KEY_6,

        /// <summary>
        /// 7
        /// </summary>
        OBS_KEY_7,

        /// <summary>
        /// 8
        /// </summary>
        OBS_KEY_8,

        /// <summary>
        /// 9
        /// </summary>
        OBS_KEY_9,

        /// <summary>
        /// Numeric =
        /// </summary>
        OBS_KEY_NUMEQUAL,

        /// <summary>
        /// Numeric *
        /// </summary>
        OBS_KEY_NUMASTERISK,

        /// <summary>
        /// Numeric +
        /// </summary>
        OBS_KEY_NUMPLUS,

        /// <summary>
        /// Numeric ,
        /// </summary>
        OBS_KEY_NUMCOMMA,

        /// <summary>
        /// Numeric -
        /// </summary>
        OBS_KEY_NUMMINUS,

        /// <summary>
        /// Numeric .
        /// </summary>
        OBS_KEY_NUMPERIOD,

        /// <summary>
        /// Numeric /
        /// </summary>
        OBS_KEY_NUMSLASH,

        /// <summary>
        /// Numeric 0
        /// </summary>
        OBS_KEY_NUM0,

        /// <summary>
        /// Numeric 1
        /// </summary>
        OBS_KEY_NUM1,

        /// <summary>
        /// Numeric 2
        /// </summary>
        OBS_KEY_NUM2,

        /// <summary>
        /// Numeric 3
        /// </summary>
        OBS_KEY_NUM3,

        /// <summary>
        /// Numeric 4
        /// </summary>
        OBS_KEY_NUM4,

        /// <summary>
        /// Numeric 5
        /// </summary>
        OBS_KEY_NUM5,

        /// <summary>
        /// Numeric 6
        /// </summary>
        OBS_KEY_NUM6,

        /// <summary>
        /// Numeric 7
        /// </summary>
        OBS_KEY_NUM7,

        /// <summary>
        /// Numeric 8
        /// </summary>
        OBS_KEY_NUM8,

        /// <summary>
        /// Numeric 9
        /// </summary>
        OBS_KEY_NUM9,

        /// <summary>
        /// :
        /// </summary>
        OBS_KEY_COLON,

        /// <summary>
        /// ;
        /// </summary>
        OBS_KEY_SEMICOLON,

        /// <summary>
        /// '
        /// </summary>
        OBS_KEY_QUOTE,

        /// <summary>
        /// Less
        /// </summary>
        OBS_KEY_LESS,

        /// <summary>
        /// =
        /// </summary>
        OBS_KEY_EQUAL,

        /// <summary>
        /// >
        /// </summary>
        OBS_KEY_GREATER,

        /// <summary>
        /// ?
        /// </summary>
        OBS_KEY_QUESTION,

        /// <summary>
        /// @
        /// </summary>
        OBS_KEY_AT,

        /// <summary>
        /// a
        /// </summary>
        OBS_KEY_A,

        /// <summary>
        /// b
        /// </summary>
        OBS_KEY_B,

        /// <summary>
        /// c
        /// </summary>
        OBS_KEY_C,

        /// <summary>
        /// d
        /// </summary>
        OBS_KEY_D,

        /// <summary>
        /// e
        /// </summary>
        OBS_KEY_E,

        /// <summary>
        /// f
        /// </summary>
        OBS_KEY_F,

        /// <summary>
        /// g
        /// </summary>
        OBS_KEY_G,

        /// <summary>
        /// h
        /// </summary>
        OBS_KEY_H,

        /// <summary>
        /// i
        /// </summary>
        OBS_KEY_I,

        /// <summary>
        /// j
        /// </summary>
        OBS_KEY_J,

        /// <summary>
        /// k
        /// </summary>
        OBS_KEY_K,

        /// <summary>
        /// l
        /// </summary>
        OBS_KEY_L,

        /// <summary>
        /// m
        /// </summary>
        OBS_KEY_M,

        /// <summary>
        /// n
        /// </summary>
        OBS_KEY_N,

        /// <summary>
        /// o
        /// </summary>
        OBS_KEY_O,

        /// <summary>
        /// p
        /// </summary>
        OBS_KEY_P,

        /// <summary>
        /// q
        /// </summary>
        OBS_KEY_Q,

        /// <summary>
        /// r
        /// </summary>
        OBS_KEY_R,

        /// <summary>
        /// s
        /// </summary>
        OBS_KEY_S,

        /// <summary>
        /// t
        /// </summary>
        OBS_KEY_T,

        /// <summary>
        /// u
        /// </summary>
        OBS_KEY_U,

        /// <summary>
        /// v
        /// </summary>
        OBS_KEY_V,

        /// <summary>
        /// w
        /// </summary>
        OBS_KEY_W,

        /// <summary>
        /// x
        /// </summary>
        OBS_KEY_X,

        /// <summary>
        /// y
        /// </summary>
        OBS_KEY_Y,

        /// <summary>
        /// z
        /// </summary>
        OBS_KEY_Z,

        /// <summary>
        /// [
        /// </summary>
        OBS_KEY_BRACKETLEFT,

        /// <summary>
        /// \
        /// </summary>
        OBS_KEY_BACKSLASH,

        /// <summary>
        /// ]
        /// </summary>
        OBS_KEY_BRACKETRIGHT,

        /// <summary>
        /// Ascii Circum
        /// </summary>
        OBS_KEY_ASCIICIRCUM,

        /// <summary>
        /// _
        /// </summary>
        OBS_KEY_UNDERSCORE,

        /// <summary>
        /// Quote left
        /// </summary>
        OBS_KEY_QUOTELEFT,

        /// <summary>
        /// {
        /// </summary>
        OBS_KEY_BRACELEFT,

        /// <summary>
        /// Bar
        /// </summary>
        OBS_KEY_BAR,

        /// <summary>
        /// }
        /// </summary>
        OBS_KEY_BRACERIGHT,

        /// <summary>
        /// ~
        /// </summary>
        OBS_KEY_ASCIITILDE,

        /// <summary>
        /// Non-Breaking Space
        /// </summary>
        OBS_KEY_NOBREAKSPACE,

        /// <summary>
        /// Exclamation down
        /// </summary>
        OBS_KEY_EXCLAMDOWN,

        /// <summary>
        /// Cent
        /// </summary>
        OBS_KEY_CENT,

        /// <summary>
        /// Sterling
        /// </summary>
        OBS_KEY_STERLING,

        /// <summary>
        /// Currency
        /// </summary>
        OBS_KEY_CURRENCY,

        /// <summary>
        /// Yen
        /// </summary>
        OBS_KEY_YEN,

        /// <summary>
        /// Brokenbar
        /// </summary>
        OBS_KEY_BROKENBAR,

        /// <summary>
        /// Section
        /// </summary>
        OBS_KEY_SECTION,

        /// <summary>
        /// Diaeresis
        /// </summary>
        OBS_KEY_DIAERESIS,

        /// <summary>
        /// Copyright
        /// </summary>
        OBS_KEY_COPYRIGHT,

        /// <summary>
        /// Feminine
        /// </summary>
        OBS_KEY_ORDFEMININE,

        /// <summary>
        /// Gillemot left
        /// </summary>
        OBS_KEY_GUILLEMOTLEFT,

        /// <summary>
        /// Not-sign
        /// </summary>
        OBS_KEY_NOTSIGN,

        /// <summary>
        /// Hyphen
        /// </summary>
        OBS_KEY_HYPHEN,

        /// <summary>
        /// Registered
        /// </summary>
        OBS_KEY_REGISTERED,

        /// <summary>
        /// Macron
        /// </summary>
        OBS_KEY_MACRON,

        /// <summary>
        /// Degree
        /// </summary>
        OBS_KEY_DEGREE,

        /// <summary>
        /// Plus-Minus
        /// </summary>
        OBS_KEY_PLUSMINUS,

        /// <summary>
        /// Two Superior
        /// </summary>
        OBS_KEY_TWOSUPERIOR,

        /// <summary>
        /// Three Superior
        /// </summary>
        OBS_KEY_THREESUPERIOR,

        /// <summary>
        /// Acute
        /// </summary>
        OBS_KEY_ACUTE,

        /// <summary>
        /// Mu
        /// </summary>
        OBS_KEY_MU,

        /// <summary>
        /// Paragraph
        /// </summary>
        OBS_KEY_PARAGRAPH,

        /// <summary>
        /// Period Centered
        /// </summary>
        OBS_KEY_PERIODCENTERED,

        /// <summary>
        /// Cedilla
        /// </summary>
        OBS_KEY_CEDILLA,

        /// <summary>
        /// One Superior
        /// </summary>
        OBS_KEY_ONESUPERIOR,

        /// <summary>
        /// Masculine
        /// </summary>
        OBS_KEY_MASCULINE,

        /// <summary>
        /// Guillemot Right
        /// </summary>
        OBS_KEY_GUILLEMOTRIGHT,

        /// <summary>
        /// One quarter
        /// </summary>
        OBS_KEY_ONEQUARTER,

        /// <summary>
        /// One half
        /// </summary>
        OBS_KEY_ONEHALF,

        /// <summary>
        /// Three quarters
        /// </summary>
        OBS_KEY_THREEQUARTERS,

        /// <summary>
        /// Question Down
        /// </summary>
        OBS_KEY_QUESTIONDOWN,

        /// <summary>
        /// Agave
        /// </summary>
        OBS_KEY_AGRAVE,

        /// <summary>
        /// AAcute
        /// </summary>
        OBS_KEY_AACUTE,

        /// <summary>
        /// ACircumflex
        /// </summary>
        OBS_KEY_ACIRCUMFLEX,

        /// <summary>
        /// ATilde
        /// </summary>
        OBS_KEY_ATILDE,

        /// <summary>
        /// ADiaeresis
        /// </summary>
        OBS_KEY_ADIAERESIS,

        /// <summary>
        /// ARing
        /// </summary>
        OBS_KEY_ARING,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AE,

        /// <summary>
        /// CCedilla
        /// </summary>
        OBS_KEY_CCEDILLA,

        /// <summary>
        /// EGrave
        /// </summary>
        OBS_KEY_EGRAVE,

        /// <summary>
        /// EAcute
        /// </summary>
        OBS_KEY_EACUTE,

        /// <summary>
        /// ECircumflex
        /// </summary>
        OBS_KEY_ECIRCUMFLEX,

        /// <summary>
        /// EDiaeresis
        /// </summary>
        OBS_KEY_EDIAERESIS,

        /// <summary>
        /// IGrave
        /// </summary>
        OBS_KEY_IGRAVE,

        /// <summary>
        /// IAcute
        /// </summary>
        OBS_KEY_IACUTE,

        /// <summary>
        /// ICuircumflex
        /// </summary>
        OBS_KEY_ICIRCUMFLEX,

        /// <summary>
        /// IDiaeresis
        /// </summary>
        OBS_KEY_IDIAERESIS,

        /// <summary>
        /// Eth
        /// </summary>
        OBS_KEY_ETH,

        /// <summary>
        /// NTilde
        /// </summary>
        OBS_KEY_NTILDE,

        /// <summary>
        /// OGrave
        /// </summary>
        OBS_KEY_OGRAVE,

        /// <summary>
        /// OAcute
        /// </summary>
        OBS_KEY_OACUTE,

        /// <summary>
        /// OCircumflex
        /// </summary>
        OBS_KEY_OCIRCUMFLEX,

        /// <summary>
        /// OTilde
        /// </summary>
        OBS_KEY_OTILDE,

        /// <summary>
        /// ODiaeresis
        /// </summary>
        OBS_KEY_ODIAERESIS,

        /// <summary>
        /// Mutiply
        /// </summary>
        OBS_KEY_MULTIPLY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_OOBLIQUE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UGRAVE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UACUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UCIRCUMFLEX,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UDIAERESIS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_YACUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_THORN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SSHARP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DIVISION,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_YDIAERESIS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MULTI_KEY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CODEINPUT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SINGLECANDIDATE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MULTIPLECANDIDATE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PREVIOUSCANDIDATE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MODE_SWITCH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KANJI,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MUHENKAN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HENKAN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ROMAJI,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HIRAGANA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KATAKANA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HIRAGANA_KATAKANA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ZENKAKU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANKAKU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ZENKAKU_HANKAKU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TOUROKU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MASSYO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KANA_LOCK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KANA_SHIFT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EISU_SHIFT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EISU_TOGGLE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_START,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_END,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_HANJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_JAMO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_ROMAJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_JEONJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_BANJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_PREHANJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_POSTHANJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUL_SPECIAL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_GRAVE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_ACUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_CIRCUMFLEX,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_TILDE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_MACRON,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_BREVE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_ABOVEDOT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_DIAERESIS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_ABOVERING,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_floatACUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_CARON,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_CEDILLA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_OGONEK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_IOTA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_VOICED_SOUND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_SEMIVOICED_SOUND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_BELOWDOT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_HOOK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DEAD_HORN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BACK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FORWARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_STOP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_REFRESH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VOLUMEDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VOLUMEMUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VOLUMEUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BASSBOOST,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BASSUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BASSDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TREBLEUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TREBLEDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIAPLAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIASTOP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIAPREVIOUS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIANEXT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIARECORD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIAPAUSE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIATOGGLEPLAYPAUSE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HOMEPAGE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FAVORITES,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SEARCH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_STANDBY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_OPENURL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHMAIL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHMEDIA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH0,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH3,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH4,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH5,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH6,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH7,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCH9,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHC,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHG,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LAUNCHH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MONBRIGHTNESSUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MONBRIGHTNESSDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KEYBOARDLIGHTONOFF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KEYBOARDBRIGHTNESSUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_KEYBOARDBRIGHTNESSDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_POWEROFF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_WAKEUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EJECT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SCREENSAVER,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_WWW,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEMO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LIGHTBULB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SHOP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HISTORY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ADDFAVORITE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HOTLINKS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BRIGHTNESSADJUST,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FINANCE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_COMMUNITY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AUDIOREWIND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BACKFORWARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_APPLICATIONLEFT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_APPLICATIONRIGHT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BOOK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CALCULATOR,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TODOLIST,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CLEARGRAB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CLOSE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_COPY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CUT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DISPLAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DOS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_DOCUMENTS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EXCEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EXPLORER,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_GAME,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_GO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ITOUCH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LOGOFF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MARKET,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEETING,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MENUKB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MENUPB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MYSITES,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_NEWS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_OFFICEHOME,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_OPTION,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PASTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PHONE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CALENDAR,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_REPLY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_RELOAD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ROTATEWINDOWS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ROTATIONPB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ROTATIONKB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SAVE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SEND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SPELL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SPLITSCREEN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SUPPORT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TASKPANE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TERMINAL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TOOLS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TRAVEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VIDEO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_WORD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_XFER,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ZOOMIN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ZOOMOUT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AWAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MESSENGER,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_WEBCAM,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MAILFORWARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PICTURES,

        /// <summary>
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MUSIC,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BATTERY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_BLUETOOTH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_WLAN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UWB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AUDIOFORWARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AUDIOREPEAT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AUDIORANDOMPLAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SUBTITLE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_AUDIOCYCLETRACK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TIME,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HIBERNATE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VIEW,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TOPMENU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_POWERDOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SUSPEND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CONTRASTADJUST,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_MEDIALAST,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CALL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CAMERA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CAMERAFOCUS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CONTEXT1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CONTEXT2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CONTEXT3,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CONTEXT4,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FLIP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_HANGUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_NO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SELECT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_YES,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_TOGGLECALLHANGUP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VOICEDIAL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_LASTNUMBERREDIAL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_EXECUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PRINTER,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PLAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_SLEEP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_ZOOM,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_CANCEL,


        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_OPEN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FIND,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_REDO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_UNDO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_FRONT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_PROPS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_CANCEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x07,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x0A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x0B,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x0E,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x0F,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x16,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_JUNJA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_FINAL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x1A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ACCEPT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_MODECHANGE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_SELECT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_PRINT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_EXECUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_HELP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x30,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x31,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x32,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x33,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x34,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x35,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x36,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x37,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x38,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x39,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3B,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3C,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3D,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3E,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x3F,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x40,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x41,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x42,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x43,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x44,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x45,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x46,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x47,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x48,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x49,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4B,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4C,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4D,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4E,

        /// <summary>
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x4F,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x50,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x51,

        /// <summary>
        /// 
        /// </summary>

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x52,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x53,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x54,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x55,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x56,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x57,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x58,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x59,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x5A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LWIN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_RWIN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_APPS,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x5E,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_SLEEP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_SEPARATOR,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x88,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x89,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8B,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8C,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8D,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8E,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x8F,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_FJ_JISHO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_FJ_LOYA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_FJ_ROYA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x97,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x98,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x99,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9A,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9B,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9C,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9D,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9E,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0x9F,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LSHIFT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_RSHIFT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LCONTROL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_RCONTROL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LMENU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_RMENU,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_BACK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_FORWARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_REFRESH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_STOP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_SEARCH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_FAVORITES,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_BROWSER_HOME,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_VOLUME_MUTE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_VOLUME_DOWN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_VOLUME_UP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_MEDIA_NEXT_TRACK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_MEDIA_PREV_TRACK,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_MEDIA_STOP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_MEDIA_PLAY_PAUSE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LAUNCH_MAIL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LAUNCH_MEDIA_SELECT,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LAUNCH_APP1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_LAUNCH_APP2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xB8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xB9,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC3,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC4,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC5,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC6,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC7,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xC9,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCB,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCC,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCD,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCE,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xCF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD0,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD3,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD4,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD5,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD6,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD7,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xD9,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xDA,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xE0,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_AX,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ICO_HELP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ICO_00,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_PROCESSKEY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ICO_CLEAR,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_PACKET,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_0xE8,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_RESET,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_JUMP,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_PA1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_PA2,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_PA3,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_WSCTRL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_CUSEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_ATTN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_FINISH,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_COPY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_AUTO,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_ENLW,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ATTN,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_CRSEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_EXSEL,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_EREOF,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_PLAY,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_ZOOM,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_NONAME,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_PA1,

        /// <summary>
        /// 
        /// </summary>
        OBS_KEY_VK_OEM_CLEAR,
    }
}
