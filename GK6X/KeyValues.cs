﻿using MiniJSON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GK6X
{
    public static class KeyValues
    {
        public static List<Group> Groups = new List<Group>();
        public static Dictionary<uint, Key> Keys = new Dictionary<uint, Key>();
        public static Dictionary<int, Key> KeysByLogicCode = new Dictionary<int, Key>();

        /// <summary>
        /// These map full driver values (4 bytes long) to the individual driver key codes (1 byte long)
        /// This is only for actual keys (things like VolumeUp don't appear here)
        /// </summary>
        public static Dictionary<uint, byte> LongToShortDriverValues = new Dictionary<uint, byte>();
        public static Dictionary<byte, uint> ShortToLongDriverValues = new Dictionary<byte, uint>();

        /// <summary>
        /// Unused key valey / invalid key value. Used for keys which aren't mapped on the keyboard.
        /// </summary>
        public const uint UnusedKeyValue = 0xFFFFFFFF;

        public static bool Load()
        {
            const string fileName = "keys.json";
            string filePath = Path.Combine(Program.DataBasePath, fileName);

            Groups.Clear();
            Keys.Clear();
            KeysByLogicCode.Clear();

            LongToShortDriverValues.Clear();
            ShortToLongDriverValues.Clear();
            foreach (DriverValue value in Enum.GetValues(typeof(DriverValue)))
            {
                if (GetKeyType((uint)value) == DriverValueType.Key &&
                    GetKeyModifier((uint)value) == DriverValueModifer.None)
                {
                    byte shortValue = GetKeyData1((uint)value);
                    LongToShortDriverValues[(uint)value] = shortValue;
                    ShortToLongDriverValues[shortValue] = (uint)value;
                }
            }

            if (File.Exists(filePath))
            {
                char[] driverValueSplitChars = { ',' };

                List<object> keyGroups = Json.Deserialize(File.ReadAllText(filePath)) as List<object>;
                foreach (object keyGroupObj in keyGroups)
                {
                    Dictionary<string, object> keyGroupMembers = keyGroupObj as Dictionary<string, object>;
                    Group group = new Group();
                    Groups.Add(group);
                    Json.TryGetValue(keyGroupMembers, "keytype", out group.KeyType);
                    Json.TryGetValue(keyGroupMembers, "pname", out group.PName);
                    string groupTitleLang;
                    if (Json.TryGetValue(keyGroupMembers, "title_lang", out groupTitleLang))
                    {
                        group.Title = new LocalizedString(groupTitleLang);
                    }
                    List<object> keyGroupKeys;
                    if (Json.TryGetValue(keyGroupMembers, "keys", out keyGroupKeys))
                    {
                        // This is a bit of a mouthful... (array->map->array->map)
                        foreach (object lineKeysListObj in keyGroupKeys)
                        {
                            Dictionary<string, object> lineKeysListDict = lineKeysListObj as Dictionary<string, object>;
                            List<object> lineKeysObjs;
                            if (Json.TryGetValue(lineKeysListDict, "linekeys", out lineKeysObjs))
                            {
                                foreach (object lineKeysObj in lineKeysObjs)
                                {
                                    Dictionary<string, object> keyInfo = lineKeysObj as Dictionary<string, object>;

                                    Key key = new Key(group);
                                    long locationCode;
                                    long logicCode;
                                    string driverValueStr;
                                    string langTitle;
                                    Json.TryGetValue(keyInfo, "Name", out key.Name);
                                    if (Json.TryGetValue(keyInfo, "LocationCode", out locationCode))
                                    {
                                        key.LocationCode = (int)locationCode;
                                    }
                                    if (Json.TryGetValue(keyInfo, "LogicCode", out logicCode))
                                    {
                                        key.LogicCode = (int)logicCode;
                                    }
                                    if (Json.TryGetValue(keyInfo, "DriverValue", out driverValueStr) && !string.IsNullOrEmpty(driverValueStr))
                                    {
                                        if (driverValueStr.StartsWith("0x"))
                                        {
                                            long driverValue;
                                            if (long.TryParse(driverValueStr.Substring(2), NumberStyles.HexNumber, null, out driverValue))
                                            {
                                                key.DriverValue = (uint)driverValue;
                                                if (Keys.ContainsKey(key.DriverValue))
                                                {
                                                    Debug.WriteLine("[WARNING] Duplicate key 0x" + key.DriverValue.ToString("X8") + " (" + fileName + ")");
                                                }
                                                Keys[key.DriverValue] = key;
                                                KeysByLogicCode[key.LogicCode] = key;
                                            }
                                        }
                                        else
                                        {
                                            string[] splitted = driverValueStr.Split(driverValueSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                            key.DriverValueArray = new int[splitted.Length];
                                            for (int i = 0; i < splitted.Length; i++)
                                            {
                                                int.TryParse(splitted[i], out key.DriverValueArray[i]);
                                            }
                                        }
                                    }
                                    if (Json.TryGetValue(keyInfo, "LangTitle", out langTitle))
                                    {
                                        key.Title = new LocalizedString(langTitle);
                                    }
                                }
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public static DriverValueModifer GetKeyModifier(uint driverValue)
        {
            return (DriverValueModifer)GetKeyData2(driverValue);
        }

        public static DriverValueType GetKeyType(uint driverValue)
        {
            return (DriverValueType)(driverValue >> 16);
        }

        public static bool IsKeyModifier(uint driverValue)
        {
            return GetKeyType(driverValue) == DriverValueType.Key &&
                GetKeyModifier(driverValue) != DriverValueModifer.None &&
                GetKeyData1(driverValue) == 0;
        }

        public static DriverValueMouseButton GetMouseButton(uint driverValue)
        {
            if (GetKeyType(driverValue) == DriverValueType.Mouse)
            {
                return (DriverValueMouseButton)GetKeyData2(driverValue);
            }
            return DriverValueMouseButton.None;
        }

        /// <summary>
        /// Used to hold the value of the key and modifier keys (as well as other things on other key types)
        /// </summary>
        public static ushort GetKeyData(uint driverValue)
        {
            return (ushort)(driverValue & 0xFFFF);
        }

        /// <summary>
        /// Used to hold the actual value of the key
        /// </summary>
        public static byte GetKeyData1(uint driverValue)
        {
            return (byte)((driverValue >> 8) & 0xFF);
        }

        /// <summary>
        /// Used to hold additional data (such as the modifiers / macro index / keyboard layer / mouse button)
        /// </summary>
        public static byte GetKeyData2(uint driverValue)
        {
            return (byte)(driverValue & 0xFF);
        }

        public static byte GetShortDriverValue(uint longValue)
        {
            byte result;
            LongToShortDriverValues.TryGetValue(longValue, out result);
            return result;
        }

        public static uint GetLongDriverValue(byte shortValue)
        {
            uint result;
            ShortToLongDriverValues.TryGetValue(shortValue, out result);
            return result;
        }

        public class Group
        {
            public string KeyType;
            public string PName;
            public LocalizedString Title;
            public List<Key> Keys;

            public Group()
            {
                Keys = new List<Key>();
            }
        }

        public class Key
        {
            /// <summary>
            /// The owning group
            /// </summary>
            public Group Group;
            /// <summary>
            /// Where the key appears visually
            /// </summary>
            public int LocationCode;
            /// <summary>
            /// Where the index of the key as defined in the keyboard profile.json
            /// </summary>
            public int LogicCode;
            /// <summary>
            /// The name of the key
            /// </summary>
            public string Name;
            /// <summary>
            /// The localized name of the key (can be null)
            /// </summary>
            public LocalizedString Title;
            /// <summary>
            /// The key value which the keyboard firmware understands
            /// </summary>
            public uint DriverValue;
            /// <summary>
            /// Used for disabling multiple keys
            /// </summary>
            public int[] DriverValueArray;

            public Key(Group group)
            {
                LocationCode = -1;
                LogicCode = -1;
                DriverValue = UnusedKeyValue;
                Group = group;
                group.Keys.Add(this);
            }
        }
    }

    // Fn key note:
    // There isn't any way to reprogram the Fn key. It's technically possible to assign a key to the Fn key but the
    // base functionality of the Fn key cannot be overridden. Assigning a key to the Fn key only works on the Fn key set
    // as pressing Fn instantly switches to the Fn key set. Assigning a key to the Fn key in this way is completely
    // pointless, but it gives some insight into how things work.

    // Base layer note:
    // The base layer is programmable, but the Fn key on base layer doesn't seem to be (possibly disabled out of fear
    // that a user would unknowingly lock themselves out somehow?) This kind of sucks!

    /// <summary>
    /// The key driver values as defined in the files
    /// </summary>
    public enum DriverValue : uint
    {
        None = 0,

        ///////////////////////////
        // primary
        ///////////////////////////

        Esc = 0x02002900,
        /// <summary>
        /// Disables the key
        /// </summary>
        Disabled = 0x02000000,
        F1 = 0x02003A00,
        F2 = 0x02003B00,
        F3 = 0x02003C00,
        F4 = 0x02003D00,
        F5 = 0x02003E00,
        F6 = 0x02003F00,
        F7 = 0x02004000,
        F8 = 0x02004100,
        F9 = 0x02004200,
        F10 = 0x02004300,
        F11 = 0x02004400,
        F12 = 0x02004500,
        PrintScreen = 0x02004600,//PS
        ScrollLock = 0x02004700,//SL
        Pause = 0x02004800,//PB
        // --- end line ---
        /// <summary>
        /// '`' key (backtick/grave/tilde)
        /// </summary>
        BackTick = 0x02003500,
        // D = decimal (base 10)
        D1 = 0x02001E00,
        D2 = 0x02001F00,
        D3 = 0x02002000,
        D4 = 0x02002100,
        D5 = 0x02002200,
        D6 = 0x02002300,
        D7 = 0x02002400,
        D8 = 0x02002500,
        D9 = 0x02002600,
        D0 = 0x02002700,
        Subtract = 0x02002D00,
        Add = 0x02002E00,
        Backspace = 0x02002A00,
        Insert = 0x02004900,
        Home = 0x02004A00,
        PageUp = 0x02004B00,
        // --- end line ---
        Tab = 0x02002B00,
        Q = 0x02001400,
        W = 0x02001A00,
        E = 0x02000800,
        R = 0x02001500,
        T = 0x02001700,
        Y = 0x02001C00,
        U = 0x02001800,
        I = 0x02000C00,
        O = 0x02001200,
        P = 0x02001300,
        OpenSquareBrace = 0x02002F00,
        CloseSquareBrace = 0x02003000,
        Backslash = 0x02003100,// also 0x02003200
        Delete = 0x02004C00,
        End = 0x02004D00,
        PageDown = 0x02004E00,
        // --- end line ---
        CapsLock = 0x02003900,
        A = 0x02000400,
        S = 0x02001600,
        D = 0x02000700,
        F = 0x02000900,
        G = 0x02000A00,
        H = 0x02000B00,
        J = 0x02000D00,
        K = 0x02000E00,
        L = 0x02000F00,
        Semicolon = 0x02003300,
        Quotes = 0x02003400,
        Enter = 0x02002800,
        // --- end line ---
        LShift = 0x02000002,
        /// <summary>
        /// Key between left shift and Z
        /// </summary>
        AltBackslash = 0x02006400,
        Z = 0x02001D00,
        X = 0x02001B00,
        C = 0x02000600,
        V = 0x02001900,
        B = 0x02000500,
        N = 0x02001100,
        M = 0x02001000,
        Comma = 0x02003600,
        Period = 0x02003700,
        /// <summary>
        /// '/' and '?'
        /// </summary>
        Slash = 0x02003800,
        RShift = 0x02000020,
        Up = 0x02005200,
        LCtrl = 0x02000001,
        LWin = 0x02000008,
        LAlt = 0x02000004,
        Space = 0x02002C00,
        RAlt = 0x02000040,
        RWin = 0x02000080,
        Menu = 0x02006500,
        RCtrl = 0x02000010,
        Left = 0x02005000,
        Down = 0x02005100,
        Right = 0x02004F00,

        ///////////////////////////
        // numpad
        ///////////////////////////

        NumLock = 0x02005300,
        NumPadSlash = 0x02005400,
        NumPadAsterisk = 0x02005500,
        NumPadSubtract = 0x02005600,
        // --- end line ---
        NumPad7 = 0x02005F00,// home
        NumPad8 = 0x02006000,// up
        NumPad9 = 0x02006100,// pageup
        NumPadAdd = 0x02005700,
        // --- end line ---
        NumPad4 = 0x02005C00,// left
        NumPad5 = 0x02005D00,
        NumPad6 = 0x02005E00,// right
        // --- end line ---
        NumPad1 = 0x02005900,// end
        NumPad2 = 0x02005A00,// down
        NumPad3 = 0x02005B00,// pagedown
        // --- end line ---
        NumPad0 = 0x02006200,
        NumPadPeriod = 0x02006300,// del
        NumPadEnter = 0x02005800,

        ///////////////////////////
        // media
        ///////////////////////////

        OpenMediaPlayer = 0x03000183,
        MediaPlayPause = 0x030000CD,
        MediaStop = 0x030000B7,
        // --- end line ---
        MediaPrevious = 0x030000B6,
        MediaNext = 0x030000B5,
        // --- end line ---
        VolumeUp = 0x030000E9,
        VolumeDown = 0x030000EA,
        VolumeMute = 0x030000E2,

        ///////////////////////////
        // system
        ///////////////////////////

        BrowserSearch = 0x03000221,
        BrowserStop = 0x03000226,
        BrowserBack = 0x03000224,
        BrowserForward = 0x03000225,
        BrowserRefresh = 0x03000227,
        BrowserFavorites = 0x0300022A,
        BrowserHome = 0x03000223,
        // --- end line ---
        OpenEmail = 0x0300018A,
        OpenMyComputer = 0x03000194,
        OpenCalculator = 0x03000192,
        // --- end line ---
        Copy = 0x02000601,
        Paste = 0x02001901,
        Screenshot = 0x02004600,

        ///////////////////////////
        // mouse
        ///////////////////////////

        MouseLClick = 0x01010001,
        MouseRClick = 0x01010002,
        MouseMClick = 0x01010004,
        MouseBack = 0x01010008,
        MouseAdvance = 0x01010010,

        ///////////////////////////
        // keyboard layers (temporary switch)
        ///////////////////////////

        TempSwitchLayerBase = 0x0a070001,// std / standard
        TempSwitchLayer1 = 0x0a070002,
        TempSwitchLayer2 = 0x0a070003,
        TempSwitchLayer3 = 0x0a070004,
        TempSwitchLayerDriver = 0x0a070005,// TODO: Verify this exists

        ///////////////////////////
        // The following values aren't defined in any files in the GK6X software
        ///////////////////////////

        // https://www.w3.org/TR/uievents-code/

        Power = 0x02006600,// keycode 255 (w3:"Power")
        Clear = 0x02006700,// The CLEAR key
        F13 = 0x02006800,
        F14 = 0x02006900,
        F15 = 0x02006A00,
        F16 = 0x02006B00,
        F17 = 0x02006C00,
        F18 = 0x02006D00,
        F19 = 0x02006E00,
        F20 = 0x02006F00,
        F21 = 0x02007000,
        F22 = 0x02007100,
        F23 = 0x02007200,
        F24 = 0x02007300,
        NumPadComma = 0x02008500,// keycode 194 (w3:"NumpadComma")
        IntlRo = 0x02008700,// keycode 193 (w3:"IntlRo")
        KanaMode = 0x02008800,// keycode 255 (w3:"KanaMode")
        IntlYen = 0x02008900,// keycode 255 (w3:"IntlYen")
        Convert = 0x02008A00,// keycode 255 (w3:"Convert")
        NonConvert = 0x02008B00,// keycode 235 (w3:"NonConvert")
        //0x02008C00,// keycode 234 - not sure what this is
        Lang3 = 0x02009200,// keycode 255 (w3:"Lang3")
        Lang4 = 0x02009300,// keycode 255 (w3:"Lang4")
        //F24 = 0x02009400,// keycode 135 (w3:"F24") (duplicate)

        //0x0A020001 - ?
        ToggleLockWindowsKey = 0x0A020002,// Toggles a lock on the windows key
        ToggleBluetooth = 0x0A020007,
        //0x0A020006 - ?
        ToggleBluetoothNoLED = 0x0A020008,// Toggles bluetooth (and disables the bluetooth LED until manually toggled)

        // These are the same as pressing the layer buttons (pressing the button whilst it's active takes you to the base layer)
        // If you want to temporarily switch you should use the TempSwitchXXXX versions instead
        DriverLayerButton = 0x0A060001,
        Layer1Button = 0x0A060002,
        Layer2Button = 0x0A060003,
        Layer3Button = 0x0A060004,

        // 0x0A0700XX seem to do weird things with the lighting (resetting current lighting effect, disabling lighting for
        // as long as you hold down a key) - but these also seem to soft lock the keyboard shortly after, until you replug it

        NextLightingEffect = 0x09010010,// NOTE: Only works on base layer
        NextReactiveLightingEffect = 0x09010011,// NOTE: Only works on base layer
        BrightnessUp = 0x09020001,
        BrightnessDown = 0x09020002,
        LightingSpeedDecrease = 0x09030002,
        LightingSpeedIncrease = 0x09030001,
        LightingPauseResume = 0x09060001,
        ToggleLighting = 0x09060002,
        // These values were found in the GK64 firmware, but don't seem to do anything?
        //0x09010002 - ?
        //0x09011307 - ?
        //0x09010006 - ?
        //0x09010008 - ?
        //0x09010009 - ?
        //0x09010004 - ?
        //0x09010003 - ?
        //0x09010005 - ?
        //0x0901000A - ?

        // TODO: Find Bluetooth buttons 1-3
        // TODO: Find Fn value (if it even exists)
        // TODO: Find flash memory value (if it even exists)

        ///////////////////////////
        // The following values are fake and not used by the firmware.
        // These are used internally to represent non-standard keys
        // that have the same label as a standard key.
        ///////////////////////////

        MBackspace = 0xFF002A00,
        MEnter = 0xFF002800,
        MCtrl = 0xFF000001,
        MShift = 0xFF000020,
        RSpace = 0xFF002C00,
    }

    public enum DriverValueType : ushort
    {
        None = 0,
        Mouse = 0x0101,
        Key = 0x0200,
        /// <summary>
        /// Open "My Computer", calculator, etc
        /// </summary>
        System = 0x0300,
        Macro = 0x0A01,
        TempSwitchLayer = 0x0A07,
    }

    [Flags]
    public enum DriverValueModifer
    {
        None,
        LCtrl = 0x01,
        LShift = 0x02,
        LAlt = 0x04,
        LWin = 0x08,
        RCtrl = 0x10,
        RShift = 0x20,
        RAlt = 0x40,
        RWin = 0x80
    }

    [Flags]
    public enum DriverValueMouseButton
    {
        None,
        LButton = 0x01,
        RButton = 0x02,
        MButton = 0x04,
        Back = 0x08,
        Advance = 0x10
    }
}
