using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Components
{
    /// <summary>
    /// User status UI in \link Dojo.UI.DojoMenu DojoMenu \endlink
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DojoMenuStatus : MonoBehaviour
    {
        private VisualElement _UI;
        private Button _UI_name;
        private Button _UI_matchID;
        private DropdownField _UI_role;
        private Button _UI_indicator;

        private readonly Dictionary<DojoNetworkRole, int> _roleToIndex = new();
        private readonly Dictionary<int, DojoNetworkRole> _indexToRole = new();
        private int _selectedIndex = -1;

        private readonly string SYTLECLASS_RED = "IndicatorRed";
        private readonly string SYTLECLASS_GREEN = "IndicatorGreen";

        /** Invoked when exit match button is pressed */
        public event Action OnIndicatorClicked;

        /** Invoked when user selects a new role in dropdown menu */
        public event Action<DojoNetworkRole> OnRoleSwitchRequested;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _UI = root.Q<VisualElement>("StatusUI");
            _UI_name = _UI.Q<Button>("StatusName");
            _UI_matchID = _UI.Q<Button>("StatusMatchID");
            _UI_role = _UI.Q<DropdownField>("StatusRole");
            _UI_indicator = _UI.Q<Button>("StatusIndicator");

            // populate roles
            _UI_role.choices = new();
            foreach (var (name, index) in Enum.GetNames(typeof(DojoNetworkRole)).Select((p, idx) => (p, idx)))
            {
                _UI_role.choices.Add(name);
                var role = Enum.Parse<DojoNetworkRole>(name);
                _roleToIndex[role] = index;
                _indexToRole[index] = role;
            }
        }

        private void Start()
        {
            _UI_name.clickable.clicked += () => CopyTextToClipboard(UserName);
            _UI_matchID.clickable.clicked += () => CopyTextToClipboard(MatchID);
            _UI_indicator.clickable.clicked += () => OnIndicatorClicked?.Invoke();

            _UI_role.RegisterValueChangedCallback((e) =>
            {
                if (Enum.TryParse<DojoNetworkRole>(e.newValue, out var role))
                {
                    OnRoleSwitchRequested?.Invoke(role);
                    var index = _roleToIndex[role];

                    if (_selectedIndex != index)
                    {
                        _UI_role.SetValueWithoutNotify(e.previousValue);
                    }
                }
            });
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

        /** Current user name to display */
        public string UserName
        {
            get
            {
                return _UI_name.text;
            }
            set
            {
                _UI_name.text = value;
            }
        }

        /** Current match ID to display */
        public string MatchID
        {
            get
            {
                return _UI_matchID.text;
            }
            set
            {
                _UI_matchID.text = value;
            }
        }

        /** Has current user joined a match? */
        public bool Connected
        {
            set
            {
                _UI_role.SetEnabled(value);
                _UI_indicator.RemoveFromClassList(value ? SYTLECLASS_RED : SYTLECLASS_GREEN);
                _UI_indicator.AddToClassList(value ? SYTLECLASS_GREEN : SYTLECLASS_RED);
            }
        }

        /** Current user role to display */
        public DojoNetworkRole Role
        {
            get
            {
                if (_indexToRole.TryGetValue(_UI_role.index, out var role))
                {
                    return role;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                var index = _roleToIndex[value];
                _selectedIndex = index;
                _UI_role.index = index;
            }
        }

        private void CopyTextToClipboard(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }
    }
}
