using System;
using System.Collections.Generic;
using UnityEngine;

namespace Artti.ARField
{
    [Serializable]
    public class PlaceKeywordData
    {
        public string place_id;
        public string display_name;
        public string[] keywords;
    }

    [Serializable]
    public class OcrKeywordWrapper
    {
        public List<PlaceKeywordData> places;
    }

    public class OcrPlaceMatcher
    {
        private List<PlaceKeywordData> _places;

        public OcrPlaceMatcher(string jsonContent)
        {
            var wrapper = JsonUtility.FromJson<OcrKeywordWrapper>(jsonContent);
            _places = wrapper?.places ?? new List<PlaceKeywordData>();
        }

        public string Match(string[] detectedTexts)
        {
            if (detectedTexts == null) return null;

            foreach (var text in detectedTexts)
            {
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var place in _places)
                {
                    foreach (var keyword in place.keywords)
                    {
                        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            return place.place_id;
                        }
                    }
                }
            }
            return null;
        }
    }
}
