﻿// <auto-generated />
using Newtonsoft.Json;
namespace DialogueController.Client
{
    public partial class RunRandomizedDialogueObject
    {
        [JsonProperty("gxt", NullValueHandling = NullValueHandling.Ignore)]
        public string Gxt { get; set; }

        [JsonProperty("forceFrontend", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ForceFrontend { get; set; }

        [JsonProperty("ped", NullValueHandling = NullValueHandling.Ignore)]
        public int Ped { get; set; }

        [JsonProperty("line", NullValueHandling = NullValueHandling.Ignore)]
        public string Line { get; set; }

        [JsonProperty("voice", NullValueHandling = NullValueHandling.Ignore)]
        public string Voice { get; set; }

        [JsonProperty("forceRadio", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ForceRadio { get; set; }
    }

    public partial class RunRandomizedDialogueObject
    {
        public static RunRandomizedDialogueObject FromJson(string json) => JsonConvert.DeserializeObject<RunRandomizedDialogueObject>(json, Converter.Settings);
    }
}
