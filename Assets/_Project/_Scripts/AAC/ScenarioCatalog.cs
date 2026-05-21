using System;
using System.Collections.Generic;

namespace Artti.AAC
{
    [Serializable]
    public class ObjectiveDef
    {
        public string id;
        public string description;
        public string completion;
    }

    [Serializable]
    public class SlotDef
    {
        public string key;
        public string type;
        public string description;
    }

    [Serializable]
    public class SubflowDef
    {
        public string id;
        public string trigger;
        public string handling;
    }

    [Serializable]
    public class FallbackPolicy
    {
        public string condition;
        public string action;
    }

    [Serializable]
    public class ScenarioCatalog
    {
        public string scenarioId;
        public string displayName;
        public int turnLimit = 12;
        public List<ObjectiveDef> objectives = new List<ObjectiveDef>();
        public List<SlotDef> slots = new List<SlotDef>();
        public List<SubflowDef> subflows = new List<SubflowDef>();
        public List<FallbackPolicy> fallbacks = new List<FallbackPolicy>();
    }

    [Serializable]
    public class ScenarioCatalogBundle
    {
        public List<ScenarioCatalog> scenarios = new List<ScenarioCatalog>();
    }
}
