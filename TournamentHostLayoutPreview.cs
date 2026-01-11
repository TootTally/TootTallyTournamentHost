using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using TootTallyCore.Graphics;
using TootTallyCore.Utils.TootTallyNotifs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.AI;
using UnityEngine.UI;

namespace TootTallyTournamentHost
{
    public class TournamentHostLayoutPreview
    {
        public const float LAYOUT_SIZE_MULT = .5f;
        public const float SPACING_MARGIN = 2f;

        private GameObject _layoutPanel;
        private GridLayoutGroup _gridLayout;
        private List<GameObject> _itemList;
        private int[][] userIDsMatrix;
        private int[][] configUserIdsMatrix;
        private TMP_Text[][] _usernameText;
        private Coroutine _currentUsernameCoroutine;
        private bool _isEnabled;

        public TournamentHostLayoutPreview(Transform parent)
        {
            _itemList = new List<GameObject>();
            _layoutPanel = GameObjectFactory.CreateClickableImageHolder(parent, Vector2.zero, new Vector2(Screen.width * LAYOUT_SIZE_MULT, Screen.height * LAYOUT_SIZE_MULT), new Sprite(), "LayoutPreviewPanel", null);
            if (_layoutPanel.TryGetComponent(out EventTrigger t))
                GameObject.DestroyImmediate(t);
            _gridLayout = _layoutPanel.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = Vector2.one * SPACING_MARGIN;
            _gridLayout.childAlignment = TextAnchor.MiddleCenter;
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            UpdateLayout();
        }

        public void UpdateLayout(float f) => UpdateLayout();

        public void UpdateLayout()
        {
            if (!_isEnabled) return;
            float horizontalScreenCount = (int)Plugin.Instance.HorizontalScreenCount.Value;
            float horizontalRatio = Screen.width * LAYOUT_SIZE_MULT / horizontalScreenCount;
            float verticalScreenCount = (int)Plugin.Instance.VerticalScreenCount.Value;
            float verticalRatio = Screen.height * LAYOUT_SIZE_MULT / verticalScreenCount;
            _gridLayout.cellSize = new Vector2(horizontalRatio - SPACING_MARGIN, verticalRatio - SPACING_MARGIN);
            _gridLayout.constraintCount = (int)horizontalScreenCount;
            _gridLayout.childAlignment = TextAnchor.LowerLeft;
            _gridLayout.startCorner = GridLayoutGroup.Corner.LowerLeft;
            foreach (var item in _itemList)
                GameObject.DestroyImmediate(item);
            _itemList.Clear();
            configUserIdsMatrix = TournamentHostManager.ConvertStringToMatrix(Plugin.Instance.UserIDs.Value);
            userIDsMatrix = new int[(int)verticalScreenCount][];
            _usernameText = new TMP_Text[(int)verticalScreenCount][];
            for (int i = 0; i < verticalScreenCount; i++)
            {
                userIDsMatrix[i] = new int[(int)horizontalScreenCount];
                _usernameText[i] = new TMP_Text[(int)horizontalScreenCount];
                for (int j = 0; j < horizontalScreenCount; j++)
                {
                    if (configUserIdsMatrix.Length > i && configUserIdsMatrix[i].Length > j && configUserIdsMatrix[i][j] != 0)
                        userIDsMatrix[i][j] = configUserIdsMatrix[i][j];
                    else
                        userIDsMatrix[i][j] = 0;
                    _itemList.Add(AddScreenToLayout(i, j));
                }
            }
        }

        public GameObject AddScreenToLayout(int verticalIndex, int horizontalIndex)
        {
            var item = GameObjectFactory.CreateClickableImageHolder(_layoutPanel.transform, Vector2.zero, Vector2.one, new Sprite(), "ScreenItem", null);
            if (item.TryGetComponent(out EventTrigger t))
                GameObject.DestroyImmediate(t);
            item.GetComponent<Image>().color = Color.gray;
            var input = GameObjectFactory.CreateInputField(item.transform, Vector2.zero, new Vector2(60, 60), "ScreenInputField", false);

            input.transform.Find("Image").GetComponent<RectTransform>().sizeDelta = new Vector2(60, 2);
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchorMin = inputRect.anchorMax = Vector2.one * .5f;
            var textRect = input.transform.Find("Text").GetComponent<RectTransform>();
            textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, 0);
            textRect.sizeDelta = new Vector2(60, 20);

            var labelRect = textRect.transform.Find("TextLabel").GetComponent<RectTransform>();
            labelRect.pivot = new Vector2(.5f, 0);
            labelRect.sizeDelta = new Vector2(200, 50);
            labelRect.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Bottom;

            var usernameText = _usernameText[verticalIndex][horizontalIndex] = GameObjectFactory.CreateSingleText(item.transform, $"Username{verticalIndex}-{horizontalIndex}", "None");
            usernameText.alignment = TextAlignmentOptions.Center;
            usernameText.rectTransform.anchoredPosition = new Vector2(0, -80);
            usernameText.rectTransform.anchorMin = usernameText.rectTransform.anchorMax = usernameText.rectTransform.pivot = Vector2.one * .5f;
            usernameText.rectTransform.sizeDelta = new Vector2(200, 0);

            if (userIDsMatrix.Length > verticalIndex && userIDsMatrix[verticalIndex].Length > horizontalIndex && userIDsMatrix[verticalIndex][horizontalIndex] != 0)
            {
                input.text = userIDsMatrix[verticalIndex][horizontalIndex].ToString();
                SetUserName(userIDsMatrix[verticalIndex][horizontalIndex], verticalIndex, horizontalIndex);
            }
            else
                input.text = $"{horizontalIndex} - {verticalIndex}";
            input.onValueChanged.AddListener(value => OnInputFieldValueChange(value, verticalIndex, horizontalIndex));
            return item;
        }

        public void OnInputFieldValueChange(string value, int vertIndex, int horIndex)
        {
            if (!int.TryParse(value, out int id))
            {
                TootTallyNotifManager.DisplayNotif($"ID {horIndex}:{vertIndex} was not a number.");
                return;
            }
            userIDsMatrix[vertIndex][horIndex] = id;
            SetUserName(id, vertIndex, horIndex, true);

            Plugin.Instance.UserIDs.Value = TournamentHostManager.ConvertMatrixToString(userIDsMatrix);
            Plugin.LogInfo($"ID Config updated to {Plugin.Instance.UserIDs.Value}");
        }

        public void SetUserName(int id, int vertIndex, int horIndex, bool stopCoroutines = false)
        {
            if (_currentUsernameCoroutine != null && stopCoroutines) Plugin.Instance.StopCoroutine(_currentUsernameCoroutine);
            if (_usernameText == null || _usernameText[vertIndex][horIndex] == null) return;
            if (id == 0)
                _usernameText[vertIndex][horIndex].text = "None";
            else
                _currentUsernameCoroutine = Plugin.Instance.StartCoroutine(TootTallyCore.APIServices.TootTallyAPIService.GetUserFromID(id, user =>
                {
                    _usernameText[vertIndex][horIndex].text = user.username;
                    _currentUsernameCoroutine = null;
                }));
        }

        public void Hide()
        {
            _isEnabled = false;
            _layoutPanel.SetActive(false);
        }
        public void Show()
        {
            _isEnabled = true;
            _layoutPanel.SetActive(true);
        }
    }
}
