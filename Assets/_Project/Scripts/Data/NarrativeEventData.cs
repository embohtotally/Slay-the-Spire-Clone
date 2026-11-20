using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    [Serializable]
    public class NarrativeEventChoice
    {
        public string Label = "Investigate";
        public string OutcomePreview = "[Gain 50 Gold]";
        [TextArea(2, 4)] public string ResultStory = "You find a pouch of glinting coins tucked behind the moss.";

        [Header("Requirements")]
        [Min(0)] public int RequiredMinGold = 0;
        [Min(0)] public int RequiredMinHealth = 0;

        [Header("Effects")]
        public int GoldDelta = 0;
        public int HealthDelta = 0;
        public int MaxHealthDelta = 0;
        public int StressDelta = 0;
        public bool GrantRandomRelic = false;
        public bool GrantRandomCard = false;
    }

    [CreateAssetMenu(menuName = "Data/Narrative Event")]
    public class NarrativeEventData : ScriptableObject
    {
        public string Title = "Mysterious Shrine";
        public string Subtitle = "UNKNOWN ENCOUNTER";
        [TextArea(3, 8)] public string StoryText = "A quiet lantern flickers beside the winding path...";
        public Sprite Illustration;
        public List<NarrativeEventChoice> Choices = new();
    }
}

