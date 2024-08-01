using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Nakama;
using System.Data;

namespace Dojo.UI.Components
{
    /// <summary>
    /// Match clients status UI in \link Dojo.UI.DojoMenu DojoMenu \endlink
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DojoMenuClients : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset _clientItem;

        private VisualElement _UI;
        private ScrollView _UI_list;
        private Toggle _UI_display;

        private readonly string STYLECLASS_NORMAL = "Normal";
        private readonly string STYLECLASS_CLICKED = "Clicked";
        private List<IUserPresence> _clientPresences = new();
        private List<DojoNetworkRole> _clientRoles = new();
        private int _aiPlayerCount = 0;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _UI = root.Q<VisualElement>("ClientsUI");
            _UI_list = _UI.Q<ScrollView>("ClientsList");
            _UI_display = _UI.Q<Toggle>("ClientsDisplay");
        }

        private void Start()
        {
            _UI_display.SetValueWithoutNotify(false);
            _UI_display.RegisterValueChangedCallback((e) =>
            {
                OnDisplayPlayersChanged(e.newValue);
            });
            _UI_list.Clear();
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
        /// Update list of active clients in the match
        /// </summary>
        /// <param name="clients">list of clients</param>
        public void UpdateActiveClients(List<Tuple<IUserPresence, string, DojoNetworkRole>> clients, List<string> aiPlayers)
        {
            var clientToInfo = clients.ToDictionary(c => c.Item1, c => Tuple.Create(c.Item2, c.Item3));
            var clientToElement = new Dictionary<IUserPresence, VisualElement>();

            // find existing items
            for (var clientIdx = aiPlayers.Count; clientIdx < _UI_list.childCount; ++clientIdx)
            {
                var item = _UI_list.ElementAt(clientIdx);
                var presence = _clientPresences[clientIdx - aiPlayers.Count];

                if (clientToInfo.ContainsKey(presence))
                {
                    // update role
                    var role = clientToInfo[presence].Item2;
                    item.Q<Label>("Role").text = role.ToString();

                    clientToElement[presence] = item;
                    clientToInfo.Remove(presence);
                }
            }

            // create new items
            foreach (var pair in clientToInfo)
            {
                var item = _clientItem.Instantiate();

                var button = item.Q<Button>("ItemButton");
                var userName = item.Q<Label>("Name");
                var userRole = item.Q<Label>("Role");

                var user = pair.Key;
                var info = pair.Value;

                userName.text = info.Item1;
                userRole.text = info.Item2.ToString();

                if (_UI_display.value)
                {
                    item.style.display = info.Item2 == DojoNetworkRole.Viewer ? DisplayStyle.None : DisplayStyle.Flex;
                }
                else
                {
                    item.style.display = DisplayStyle.Flex;
                }

                clientToElement[user] = item;
            }

            var aiPlayerElements = _UI_list.Children().ToList().Take(_aiPlayerCount);

            _UI_list.Clear();

            foreach (var elem in aiPlayerElements)
            {
                _UI_list.Add(elem);
            }

            // add AI players first
            for (var i = _aiPlayerCount; i < aiPlayers.Count; ++i)
            {
                var item = _clientItem.Instantiate();

                var button = item.Q<Button>("ItemButton");
                var userName = item.Q<Label>("Name");
                var userRole = item.Q<Label>("Role");

                userName.text = aiPlayers[i];
                userRole.text = "AI Player";

                button.clickable.clicked += () => OnClientItemClicked(button, userName.text);

                _UI_list.Add(item);
            }

            _aiPlayerCount = aiPlayers.Count;

            // add items in order
            _clientPresences = clients.Select(c => c.Item1).ToList();
            _clientRoles = clients.Select(c => c.Item3).ToList();
            foreach (var user in _clientPresences)
            {
                _UI_list.Add(clientToElement[user]);
            }
        }

        /// <summary>
        /// Reset internal client list states
        /// </summary>
        public void ResetClientsList()
        {
            _UI_list.Clear();
            SelectedAIPlayers.Clear();
            _aiPlayerCount = 0;
            _clientPresences.Clear();
            _clientRoles.Clear();
        }

        private void OnClientItemClicked(Button button, string agentID)
        {
            if (SelectedAIPlayers.Contains(agentID))
            {
                button.RemoveFromClassList(STYLECLASS_CLICKED);
                button.AddToClassList(STYLECLASS_NORMAL);

                SelectedAIPlayers.Remove(agentID);
            }
            else
            {
                button.RemoveFromClassList(STYLECLASS_NORMAL);
                button.AddToClassList(STYLECLASS_CLICKED);

                SelectedAIPlayers.Add(agentID);
            }
        }

        private void OnDisplayPlayersChanged(bool display)
        {
            for (var idx = _aiPlayerCount; idx < _UI_list.childCount; ++idx)
            {
                var role = _clientRoles[idx - _aiPlayerCount];
                var item = _UI_list.ElementAt(idx);

                if (display)
                {
                    item.style.display = role == DojoNetworkRole.Viewer ? DisplayStyle.None : DisplayStyle.Flex;
                }
                else
                {
                    item.style.display = DisplayStyle.Flex;
                }
            }
        }

        /**
         * Selected AI players for feedback
         * \see \link Dojo.UI.Feedback.FeedbackInterface FeedbackInterface \endlink
         * \see \link Dojo.UI.DojoMenu DojoMenu \endlink
         */
        public List<string> SelectedAIPlayers { get; private set; } = new();
    }
}
