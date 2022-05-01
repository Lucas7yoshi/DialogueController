using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace DialogueController.Client
{
    public class DialogueControllerClient : BaseScript
    {

        /*
         * !!!!!!!!!!!!!!!!!! DOES NOT SUPPORT NETWORKING AT THIS MOMENT !!!!!!!!!!!!!!!!!!!!!!
         * Need to figure out the behavior to make it more consistent. (i.e doesn't play on first run)
         * Run on all clients that should hear it in the meantime.
         */
        
        public DialogueControllerClient()
        {
#if DEBUG
            EventHandlers["onClientResourceStart"] += new Action<string>(OnClientResourceStart);
#endif

            Exports.Add("startDialogue", new Action<string, Dictionary<int, string>, string, Dictionary<int, int>, bool?, bool?, bool?>(RunDialogue));
            Exports.Add("startDialogueJson", new Action<string>(RunDialogueJson));

            Exports.Add("startRandomizedDialogue", new Action<string, string, string, int, bool?, bool?>(RunRandomizedDialogue));
            Exports.Add("startRandomizedDialogueJson", new Action<string>(RunRandomizedDialogueJson));

            SetAudioFlag("EnableHeadsetBeep", true); // enables beep, this doesn't affect anything but scripted conversation and it seems the game will not do these on non-radio filtered lines.
        }
        // functions for running dialogue through commands without the need for exports.
        // Only debug builds hook this up.
        private void OnClientResourceStart(string obj)
        {
            if (obj == GetCurrentResourceName())
            {
                RegisterCommand("dialoguenormal", new Action<int, List<object>, string>(async (source, args, raw) =>
                {
                    DebugWriteLine("dialogue2");
                    // example input:
                    // /dialogue2 fximaud FXIM_FL_8 2-FIX_IMANI 3-FIX_FRANKLIN
                    // Should run
                    // RunDialogue("fximaud", new Dictionary<int, string>() { { 2, "FIX_IMANI" }, { 3, "FIX_FRANKLIN" } }, "FXIM_FL_8")
                    DebugWriteLine(raw);
                    var split = raw.Split(' ');
                    split = split.Skip(1).ToArray();
                    var gxt = split[0];
                    var line = split[1];
                    bool forceRadio = false;
                    var speakers = new Dictionary<int, string>();
                    Dictionary<int, int> peds = null;
                    for (int i = 2; i < split.Length; i += 1)
                    {
                        if (split[i] == "true")
                        {
                            forceRadio = true;
                            continue;
                        }
                        var speaker = split[i].Split('-');
                        DebugWriteLine($"{speaker[0]} - {speaker[1]}");
                        speakers.Add(ConvertCharToNum(char.Parse(speaker[0])), speaker[1]);
                        if (speaker.Length == 3)
                        {
                            //we are probably being given a ped handle aswell
                            if (peds == null)
                                peds = new Dictionary<int, int>();
                            peds.Add(ConvertCharToNum(char.Parse(speaker[0])), int.Parse(speaker[2]));
                        }
                    }
                    RunDialogue(gxt, speakers, line, peds, false, false, forceRadio);
                }), false);

                RegisterCommand("dialoguerandom", new Action<int, List<object>, string>(async (source, aargs, raw) =>
                {
                    var args = raw.Split(' ');
                    args = args.Skip(1).ToArray();
                    var gxt = args[0];
                    var line = args[1];
                    var speaker = args[2];
                    var radioFrontend = false;
                    var ped = -1;
                    if (speaker.Split('-').Count() > 2)
                    {
                        var speakersplit = speaker.Split('-');
                        ped = int.Parse(speakersplit.Last());
                        speaker = speakersplit.First();
                    }
                    else
                    {
                        var speakersplit = speaker.Split('-');
                        ped = -1;
                        speaker = speakersplit.First();
                    }
                    if (args.Length > 2)
                        if (args[3] == "true")
                            radioFrontend = true;
                    RunRandomizedDialogue(gxt, line, speaker, ped, radioFrontend, radioFrontend);
                }), false);

            }
        }


        private void RunDialogueJson(string json)
        {
            var obj = RunDialogueObject.FromJson(json);

            var speakers = new Dictionary<int, string>();
            foreach (var s in obj.Speakers)
            {
                speakers.Add(ConvertCharToNum(s.Key[0]), s.Value);
            }

            var peds = new Dictionary<int, int>();
            if (obj.Peds != null)
            {
                foreach (var p in obj.Peds)
                {
                    peds.Add(int.Parse(p.Key), p.Value);
                }
            }
            
            RunDialogue(obj.Gxt, speakers, obj.Line, peds, obj.ForcePedsVisible, obj.PhoneCall, obj.ForceRadio); ;
        }

        private void RunRandomizedDialogueJson(string json)
        {
            var obj = RunRandomizedDialogueObject.FromJson(json);

            RunRandomizedDialogue(obj.Gxt, obj.Line, obj.Voice, obj.Ped, obj.ForceRadio, obj.ForceFrontend);
        }

        public async void RunDialogue(
            string gxt,
            Dictionary<int, string> speakers,
            string line,
            Dictionary<int, int> peds = null,
            bool? forcePedsVisible = false,
            bool? phoneCall = false,
            bool? forceRadio = false)
        {
            StopScriptedConversation(true);
            ClearAdditionalText(13, true);
            ClearAdditionalText(14, true);
            await Delay(0);
            RequestAdditionalText(gxt, 13);
            RequestAdditionalTextForDlc(gxt, 14);
            var timeout = GetGameTimer() + 5000;
            while (!HasAdditionalTextLoaded(13) && !HasAdditionalTextLoaded(14))
            {
                if (GetGameTimer() >= timeout)
                {
                    throw new KeyNotFoundException($"ERROR: Timed out trying to load gxt {gxt} for line {line}.");
                }
                await Delay(0);
            }
            timeout = GetGameTimer() + 2500;
            while (GetLabelText(line + "SL") == "NULL")
            {
                if (GetGameTimer() >= timeout)
                {
                    throw new KeyNotFoundException($"ERROR: Timed out trying to verify line {line}.");
                }                    
                await Delay(0);
            }
            DebugWriteLine("Voiceline confirmed loaded and valid.");
            // find valid lines
            var lines = new List<string>();
            for (int i = 1; i < 100; i++)
            {
                var l = $"{line}_{i}A";
                var lr = GetLabelText(l);
                if (lr == "NULL" || lr == null)
                {
                    break;
                }
                lines.Add(l);
            }
            bool pedsAreTemp = peds == null || peds.Count == 0;
            if (pedsAreTemp)
            {
                peds = new Dictionary<int, int>();
                foreach (var i in speakers)
                {
                    var ped = await World.CreatePed(Game.PlayerPed.Model, Game.PlayerPed.Position, 0f);
                    peds.Add(i.Key, ped.Handle);
                    SetBlockingOfNonTemporaryEvents(ped.Handle, true);
                    if (forcePedsVisible.HasValue && forcePedsVisible.Value)
                    {
                        var gamertag = CreateFakeMpGamerTag(ped.Handle, i.Key.ToString() + "-" + i.Value, true, true, "", 1);
                    }
                    else
                    {
                        ped.IsVisible = false;
                        ped.IsCollisionEnabled = false;
                        ped.IsPainAudioEnabled = false;
                        ped.IsInvincible = true;
                        ped.IsPositionFrozen = true;
                        ped.AttachTo(Game.PlayerPed, new Vector3(0f, 0f, -2f));
                    }
                    NetworkUnregisterNetworkedEntity(ped.Handle); // this should not change when networking is eventually done
                }
            }
            var speakerList = GetLabelText($"{line}SL");
            var lineFlags = GetLabelText($"{line}LF");
            var speakList = new List<int>();
            var listenerList = new List<int>();

            var speakerListParts = new List<string>();
            for (var i = 0; i < speakerList.Length; i += 3)
                speakerListParts.Add(speakerList.Substring(i, Math.Min(3, speakerList.Length - i)));

            DebugWriteLine($"Found {lines.Count} valid lines");
            DebugWriteLine(speakerList);
            DebugWriteLine("Just speaker ids:");
            var justSpeakerOutput = "";
            foreach (var i in speakerListParts)
            {
                justSpeakerOutput += i.Substring(0, 1) + " ";
            }
            DebugWriteLine(justSpeakerOutput);

            CreateNewScriptedConversation();
            // assign peds to voices
            foreach (var s in speakers)
            {
                AddPedToConversation(s.Key, peds[s.Key], s.Value);
                SetPedCanUseAutoConversationLookat(peds[s.Key], true);
            }

            
            bool anyRadio = false;
            foreach (var l in lines)
            {
                var lidx = lines.IndexOf(l);
                var voiceline = GetLabelText(l);
                var subtitle = GetLabelText(l.TrimEnd('A')); // used for debug writeline
                var speaker = GetSpeakerFromSL(speakerList, lidx); // confirmed
                var listener = func_87(speakerList, lidx); // probably not confirmed

                // dialogue_handler functions
                var unkInt0 = func_85(speakerList, lidx);
                var bVar6 = func_81(lineFlags, lidx);
                var bVar7 = func_80(lineFlags, lidx);
                var bVar8 = func_79(lineFlags, lidx);
                var iVar12 = func_78(lineFlags, lidx);
                var radioEffect = func_77(lineFlags, lidx); // bVar9
                var bVar10 = func_76(lineFlags, lidx);
                var bVar11 = func_75(lineFlags, lidx);

                if (!anyRadio && radioEffect)
                    anyRadio = true;

                if (forceRadio.HasValue && forceRadio.Value)
                {
                    radioEffect = true;
                    bVar11 = true;
                }

                var lineSl = GetTextSubstring(speakerList, lidx * 3, lidx * 3 + 3);
                var lineLineFlags = GetTextSubstring(lineFlags, lidx * 7, lidx * 7 + 7);
                DebugWriteLine($"SL:{lineSl} LF:{lineLineFlags} | {speaker}->{listener} - {voiceline} - {subtitle}");
                AddLineToConversation(
                    speaker,
                    voiceline,
                    l.TrimEnd('A'),
                    listener,
                    unkInt0,
                    false, // hardcoded to false/true based on a label existing? leaving as false for now
                    bVar6, // bVar6, line flags func_81
                    bVar7, // bVar7 func_80
                    bVar8, // bVar8 func_79
                    iVar12, // iVar12 func_78
                    radioEffect, // bVar9
                    bVar10,
                    bVar11 /* frontend */);
            }
            if (anyRadio)
            {
                // wait some time to let radio effect intro play
                await Delay(300);
            }

            // this doesn't really have that much practical use.
            if (phoneCall.HasValue && phoneCall.Value)
            {
                StartScriptPhoneConversation(SubtitleCheck(), false);
            }
            else
            {
                DebugWriteLine("Regular conversation");
                StartScriptConversation(SubtitleCheck(), false, false, false);
            }
            if (pedsAreTemp)
            {
                while (!IsScriptedConversationOngoing())
                    await Delay(0);
                while (IsScriptedConversationOngoing())
                {
                    await Delay(0);
                }
                foreach (var p in peds)
                {
                    var pp = p.Value;
                    DeletePed(ref pp);
                }
            }
            DebugWriteLine("Hit end of RunDialogue conversation");
        }
        
        /// <summary>
        /// Plays a randomized dialogue, these are identifiable by them having label entries like the following:
        /// JH_SWITCH_01 -> JH_SWITCH_03
        /// Voiceline: JH_SWITCHA
        /// JHSWITCHSL
        /// </summary>
        /// <param name="gxt">The GXT label set to load into 13/14</param>
        /// <param name="line">The voiceline to play</param>
        /// <param name="voice">The voice of the ped, required to be correct for the voiceline.</param>
        /// <param name="ped">The ped to play it on, if <see langword="null"/> then creates a ped attached to the player.</param>
        /// <param name="forceRadio">Overrides LF flags to force playback on radio</param>
        /// <param name="forceFrontend">Overrides LF flags to force frontend playback</param>
        /// <exception cref="Exception"></exception>
        public async void RunRandomizedDialogue(
            string gxt,
            string line,
            string voice,
            int ped = -1,
            bool? forceRadio = false,
            bool? forceFrontend = false)
        {
            ClearAdditionalText(13, true);
            ClearAdditionalText(14, true);
            await Delay(0);
            RequestAdditionalText(gxt, 13);
            RequestAdditionalTextForDlc(gxt, 14);
            DebugWriteLine("Loading GXT: " + gxt);
            var timeout = GetGameTimer() + 5000;
            while (!HasAdditionalTextLoaded(13) && !HasAdditionalTextLoaded(14))
            {
                if (GetGameTimer() > timeout)
                {
                    throw new Exception("GXT load timed out");
                }                    
                await Delay(0);
            }
            DebugWriteLine("Loaded GXT");

            while (GetLabelText(line + "SL") == "NULL")
            {
                if (GetGameTimer() > timeout)
                {
                    throw new Exception($"GXT {gxt} loaded but the line {line} doesn't appear to exist.");
                }
                await Delay(0);
            }

            var speakerList = GetLabelText($"{line}SL");
            var lineFlags = GetLabelText($"{line}LF");

            bool pedIsTemp = false;
            
            if (ped == -1)
            {
                DebugWriteLine("Creating stand in ped");
                var p = await World.CreatePed(Game.PlayerPed.Model, Game.PlayerPed.Position, 0f);
                NetworkUnregisterNetworkedEntity(p.Handle);
                p.IsVisible = false;
                p.IsInvincible = true;
                p.IsCollisionEnabled = true;
                p.IsPositionFrozen = true;
                p.IsPainAudioEnabled = false;
                p.AttachTo(Game.PlayerPed, new Vector3(0f, 0f, -2f));
                ped = p.Handle;
                pedIsTemp = true;
            }

            // find all the flags
            var listener = func_87(speakerList, 0);

            var unkInt0 = func_85(speakerList, 0);
            var bVar6 = func_81(lineFlags, 0);
            var bVar7 = func_80(lineFlags, 0);
            var bVar8 = func_79(lineFlags, 0);
            var iVar12 = func_78(lineFlags, 0);
            var radioEffect = func_77(lineFlags, 0); // bVar9



            var bVar10 = func_76(lineFlags, 0);
            var bVar11 = func_75(lineFlags, 0);

            if (forceRadio.HasValue && forceRadio.Value)
                radioEffect = true;
            if (forceFrontend.HasValue && forceFrontend.Value)
                bVar11 = true;

            CreateNewScriptedConversation();
            AddPedToConversation(0, ped, voice);
            AddLineToConversation(
                0,
                GetLabelText($"{line}A"),
                line,
                listener,
                unkInt0,
                true, // hardcoded to false/true based on a label existing? leaving as false for now
                bVar6, // bVar6, line flags func_81
                bVar7, // bVar7 func_80
                bVar8, // bVar8 func_79
                iVar12, // iVar12 func_78
                radioEffect, // bVar9
                bVar10,
                bVar11 /* frontend */);

            DebugWriteLine(line);
            DebugWriteLine(GetLabelText($"{line}A"));

            // wait for 500ms if radioEffect is true to let the effect play
            if (radioEffect)
            {
                await Delay(300);
            }

            StartScriptConversation(SubtitleCheck(), false, false, false);

            DebugWriteLine("Started randomized line");
            if (pedIsTemp)
            {
                while (!IsScriptedConversationOngoing())
                    await Delay(0);
                while (IsScriptedConversationOngoing())
                    await Delay(0);
                DeletePed(ref ped);
            }
        }

        // dialogue_handler.c functions for filling in parameters from LF/SL
        // func_88
        private int GetSpeakerFromSL(string speakerList, int line)
        {
            var sVar0 = GetTextSubstring(speakerList, line * 3, line * 3 + 1);
            return ConvertCharToNum(sVar0[0]);
        }

        // most likely listener
        private int func_87(string speakerList, int line)
        {
            var sVar0 = GetTextSubstring(speakerList, line * 3 + 1, line * 3 + 2);
            return ConvertCharToNum(sVar0[0]);
        }

        // thing after listener
        private int func_85(string speakerList, int line)
        {
            var sVar0 = GetTextSubstring(speakerList, line * 3 + 2, line * 3 + 3);
            return ConvertCharToNum(sVar0[0]);
        }

        private bool func_81(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7, line * 7 + 1);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        private bool func_80(string flagList, int Line)
        {
            var sVar0 = GetTextSubstring(flagList, Line * 7 + 1, Line * 7 + 2);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        private bool func_79(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7 + 2, line * 7 + 3);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        // ??? 
        private int func_78(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7 + 3, line * 7 + 4);
            var sVar0c = sVar0[0];
            switch (sVar0c)
            {
                case '0':
                    return 1;
                case '1':
                    return 1;
                case '2':
                    return 2;
                case '3':
                    return 3;
                default:
                    return 0;
            }
        }

        private bool func_77(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7 + 4, line * 7 + 5);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        private bool func_76(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7 + 5, line * 7 + 6);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        private bool func_75(string flagList, int line)
        {
            var sVar0 = GetTextSubstring(flagList, line * 7 + 6, line * 7 + 7);
            if (AreStringsEqual(sVar0, "0"))
            {
                return false;
            }
            return true;
        }

        public IEnumerable<string> SplitStringInParts(string s, int partLength)
        {
            for (var i = 0; i < s.Length; i += partLength)
                yield return s.Substring(i, Math.Min(partLength, s.Length - i));
        }
        public int ConvertCharToNum(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            else if (c >= 'A' && c <= 'Z')
                return c - 'A' + 10;
            else
                return -1;
        }

        private bool SubtitleCheck()
        {
            DebugWriteLine(IsSubtitlePreferenceSwitchedOn().ToString());
            return IsSubtitlePreferenceSwitchedOn();
        }

        // Cleans up logs on non debug modes, might get optimized out entirely by the compiler.
        // preferable to #if DEBUG's all over the place
        private void DebugWriteLine(string msg)
        {
#if DEBUG
            Debug.WriteLine(msg);
#endif
        }
    }
}