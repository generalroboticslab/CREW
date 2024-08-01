using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Dojo.Nakama;

namespace Dojo.UI.Components
{
    /// <summary>
    /// Match list UI in \link Dojo.UI.DojoMenu DojoMenu \endlink
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DojoMenuMatches : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset _matchItem;

        private VisualElement _UI;
        private ScrollView _UI_list;
        private Button _UI_button;

        private readonly string STYLECLASS_NORMAL = "Normal";
        private readonly string STYLECLASS_CLICKED = "Clicked";

        /** Invoked when join/find match button is pressed */
        public event Action OnMatchesButtonClicked;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _UI = root.Q<VisualElement>("MatchesUI");
            _UI_list = _UI.Q<ScrollView>("MatchesList");
            _UI_button = _UI.Q<Button>("MatchesButton");
        }

        private void Start()
        {
            _UI_button.clickable.clicked += () => OnMatchesButtonClicked?.Invoke();
            UpdateButtonText();
        }

        /** Is current UI visible? */
        public bool Visible
        {
            get
            {
                return _UI.style.display == DisplayStyle.Flex;
            }
            set
            {
                _UI.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Update list of active matches on Nakama
        /// </summary>
        /// <param name="matches">list of queried matches</param>
        public void UpdateActiveMatches(List<MatchStorageData> matches)
        {
            var matchIdToMatch = matches.ToDictionary(m => m.MatchId, m => m);
            var matchIdToElement = new Dictionary<string, VisualElement>();

            // find existing items
            for (var matchIdx = 0; matchIdx < _UI_list.childCount; ++matchIdx)
            {
                var item = _UI_list.ElementAt(matchIdx);
                var matchID = item.Q<Label>("MatchID").text;

                if (matchIdToMatch.ContainsKey(matchID))
                {
                    // update match info
                    var match = matchIdToMatch[matchID];
                    item.Q<Label>("NumPlayers").text = match.NumPlayers.ToString();
                    item.Q<Label>("NumClients").text = match.NumClients.ToString();

                    // match already exists
                    matchIdToElement[matchID] = item;
                    matchIdToMatch.Remove(matchID);
                }
                else if (SelectedMatchID.Equals(matchID))
                {
                    // reset selected ID
                    SelectedMatchID = "";
                }
            }

            // create new items
            foreach (var match in matchIdToMatch.Values)
            {
                var item = _matchItem.Instantiate();

                var button = item.Q<Button>("ItemButton");
                var matchID = item.Q<Label>("MatchID");
                var numPlayers = item.Q<Label>("NumPlayers");
                var numClients = item.Q<Label>("NumClients");

                matchID.text = match.MatchId;
                numPlayers.text = match.NumPlayers.ToString();
                numClients.text = match.NumClients.ToString();
                button.clickable.clicked += () => OnMatchItemClicked(button, match.MatchId);

                matchIdToElement[match.MatchId] = item;
            }

            // add items in order
            _UI_list.Clear();
            foreach (var matchID in matches.Select(m => m.MatchId))
            {
                _UI_list.Add(matchIdToElement[matchID]);
            }

            UpdateButtonText();
        }

        /// <summary>
        /// Reset internal match list states
        /// </summary>
        public void ResetMatchesList()
        {
            _UI_list.Clear();
            SelectedMatchID = "";
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            if (string.IsNullOrEmpty(SelectedMatchID))
            {
                _UI_button.text = "New Match";
            }
            else
            {
                _UI_button.text = "Join Match";
            }
        }

        private void UnselectAllMatches()
        {
            for (var idx = 0; idx < _UI_list.childCount; ++idx)
            {
                var item = _UI_list.ElementAt(idx);
                var button = item.Q<Button>("ItemButton");

                button.RemoveFromClassList(STYLECLASS_CLICKED);
                button.AddToClassList(STYLECLASS_NORMAL);
            }
        }

        private void OnMatchItemClicked(Button button, string matchID)
        {
            UnselectAllMatches();

            if (SelectedMatchID.Equals(matchID))
            {
                SelectedMatchID = "";
            }
            else
            {
                button.RemoveFromClassList(STYLECLASS_NORMAL);
                button.AddToClassList(STYLECLASS_CLICKED);

                SelectedMatchID = matchID;
            }

            UpdateButtonText();
        }

        /** User selected match ID */
        public string SelectedMatchID { get; private set; } = "";

        /** Is current UI readonly? */
        public bool Readonly
        {
            set
            {
                _UI_button.SetEnabled(!value);
            }
        }
    }
}
