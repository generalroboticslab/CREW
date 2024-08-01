using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Dojo.UI.Components
{
    /// <summary>
    /// Nakama server connection UI in \link Dojo.UI.DojoMenu DojoMenu \endlink
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DojoMenuConnect : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _userNameLimit = 20;

        private VisualElement _UI;
        private TextField _UI_IP;
        private TextField _UI_name;
        private Button _UI_button;

        /** Invoked when connect button is clicked */
        public event Action OnConnectButtonClicked;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _UI = root.Q<VisualElement>("ConnectUI");
            _UI_IP = _UI.Q<TextField>("ConnectIP");
            _UI_name = _UI.Q<TextField>("ConnectName");
            _UI_button = _UI.Q<Button>("ConnectButton");
        }

        private void Start()
        {
            _UI_button.clickable.clicked += () => OnConnectButtonClicked?.Invoke();

            _UI_name.RegisterValueChangedCallback(ValidateUserName);
            _UI_IP.RegisterValueChangedCallback(ValidateIPAddress);

            // press enter to connect
            _UI_name.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return)
                {
                    OnConnectButtonClicked?.Invoke();
                }
            });

            UserName = "";
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

        /** Current IP address on UI */
        public string IPAddress
        {
            get
            {
                return _UI_IP.text;
            }
            set
            {
                _UI_IP.SetValueWithoutNotify(value);
            }
        }

        /** Current user name on UI */
        public string UserName
        {
            get
            {
                return _UI_name.text;
            }
            set
            {
                _UI_name.SetValueWithoutNotify(value);
            }
        }

        /** Is current UI readonly? */
        public bool Readonly
        {
            set
            {
                _UI_button.SetEnabled(!value);
                _UI_IP.isReadOnly = value;
                _UI_name.isReadOnly = value;
            }
        }

        private void ValidateUserName(ChangeEvent<string> e)
        {
            var field = e.target as TextField;

            // validate new characters
            var prev = e.previousValue;
            var next = e.newValue;
            var failed = next.Length > _userNameLimit;

            for (var idx = prev.Length; idx < next.Length && !failed; ++idx)
            {
                var ch = next[idx];
                failed = !(ch == ' ' || char.IsLetterOrDigit(ch));
            }

            if (failed)
            {
                field.SetValueWithoutNotify(prev);
            }
        }

        private void ValidateIPAddress(ChangeEvent<string> e)
        {
            var field = e.target as TextField;

            // validate IP
            var prev = e.previousValue;
            var next = e.newValue;
            var failed = false;

            for (var idx = prev.Length; idx < next.Length && !failed; ++idx)
            {
                var ch = next[idx];
                if (ch == '.')
                {
                    var dotCount = next[..idx].Count(c => c == '.');
                    failed = dotCount >= 3 || (dotCount == 0 && idx == 0) || (idx > 0 && next[idx - 1] == '.');
                }
                else if (ch >= '0' && ch <= '9')
                {
                    var lastDotIdx = next[..idx].LastIndexOf('.');
                    failed = idx - lastDotIdx >= 4;
                    if (!failed)
                    {
                        failed = !(int.TryParse(next.Substring(lastDotIdx + 1, idx - lastDotIdx), out var value) && value <= 255);
                    }
                }
                else
                {
                    failed = true;
                }
            }

            if (failed)
            {
                field.SetValueWithoutNotify(prev);
            }
        }
    }
}
