# DialogueController for CitizenFX

This is a resource for handling GTA's scripted conversations.

It supports both regular conversations and randomized lines.

It can be provided peds to run these on or none to just play the audio.

Supports C# and lua exports

# Examples

Lua:
Regular, no assigned ped
```lua
    -- Lua requires for it to be json encoded, json.encode is included in the lua runtime.
	-- This is due to Lua -> C# issues. 
	local dialogue = {
        gxt = "imblzau", -- GXT name
        line = "IMBLZ_FIRSTF", -- Line name without 1A lines and such
        speakers = {
            ["2"] = "EXECPA_FEMALE", -- Speakers, required, based on SL. needs to be accurate
        },
        peds = nil,
        --[[peds = {
            ["2"] = PlayerPedId(),
        },]]
        forcePedsVisible = false, -- used for debugging purposes only, optional
        phoneCall = false, -- optional, forces frontend
        forceRadio = false, -- optional
    }
    exports['DialogueController']:startDialogueJson(json.encode(dialogue))
```

Randomized Line:
```lua
    local dialogue = {
        gxt = "jhaud", -- gxt dictionary
        line = "JH_SWITCH", -- the randomized line, where JH_SWITCH_01 JH_SWITCH_02 exists, simply remove the _##
        voice = "Michael", -- voice of the line
        ped = -1, -- ped to play audio on, -1 means it will play on a generated ped attached to the player
    }
    exports['DialogueController']:startRandomizedDialogueJson(json.encode(dialogue))
```

C#: Does not seem functional at the moment. if you have any idea how to properly use them they just directly hook up to (or should...) to the functions.



# How to find voice lines

The best way to find voicelines is using https://github.com/root-cause/v-labels 's labels.json

Have a line in mind open the (rather large) json in your preferred text editor and CTRL+F for the subtitle

For example, say we want to find pavel's line about the loch ness monster.

CTRL-F "from movement patterns i would say"

The label for that line is HS4P_193__5, this is a weird case where it has double underscores so remove the following the get the line:

HS4P_193__5 -> HS4P_193_

Always remove _[NUMBER] or _[NUMBER]A where applicable.

Then grab the gxt dictionary by scrolling up until you see hs4paud. thats the GXT dictionary

For speakers, it is a bit more tricky and may require some guessing. if you open the _speech.dat4.nametable for that DLC you can find at the bottom the voice names.

Like HS4_PAVEL, HS4_SNIPER1. Use context to put it together. If you are running the *debug* copy, you will see speaker id's get logged in f8.
You can use these to determine the speaker ID. note that they can go beyond 9 and become like F for some lines (like the sniper)
You can only use the actual letters to specify these, it will handle the rest for you.

it will ONLY work with the correct voice.

Some basic ones are Lester, Michael, Trevor, Franklin, Lamar, NervousRon etc.
You can also find these in the decompiled scripts.

For randomized lines, simply take what is in JH_SWITCH_01 JH_SWITCH_02 and remove _## to get the voiceline name.

# "Queuing up" lines.
Simply use IsScriptedConversationOngoing() to determine and wait for lines to complete.

This resource does not offer any way to do this at the moment, but may in the future.
It in the meantime instantly kills any conversation when it is called upon.

# Known issues
There is a hitch on first run from lua using the json exports due to Newtonsoft.Json loading.