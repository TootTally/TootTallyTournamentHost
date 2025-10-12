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
            float horizontalScreenCount = (int)Plugin.Instance.HorizontalScreenCount.Value;
            float horizontalRatio = Screen.width * LAYOUT_SIZE_MULT / horizontalScreenCount;
            float verticalScreenCount = (int)Plugin.Instance.VerticalScreenCount.Value;
            float verticalRatio = Screen.height * LAYOUT_SIZE_MULT / verticalScreenCount;
            _gridLayout.cellSize = new Vector2(horizontalRatio - SPACING_MARGIN, verticalRatio - SPACING_MARGIN);
            _gridLayout.constraintCount = (int)horizontalScreenCount;
            foreach (var item in _itemList)
                GameObject.DestroyImmediate(item);
            _itemList.Clear();
            configUserIdsMatrix = TournamentHostManager.ConvertStringToMatrix(Plugin.Instance.UserIDs.Value);
            userIDsMatrix = new int[(int)verticalScreenCount][];
            for (int i = 0; i < verticalScreenCount; i++)
            {
                userIDsMatrix[i] = new int[(int)horizontalScreenCount];
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
            if (userIDsMatrix.Length > verticalIndex && userIDsMatrix[verticalIndex].Length > horizontalIndex && userIDsMatrix[verticalIndex][horizontalIndex] != 0)
                input.text = userIDsMatrix[verticalIndex][horizontalIndex].ToString();
            else
                input.text = $"{horizontalIndex} - {verticalIndex}";
            input.onValueChanged.AddListener(value => OnInputFieldValueChange(value, verticalIndex, horizontalIndex));
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
            Plugin.Instance.UserIDs.Value = TournamentHostManager.ConvertMatrixToString(userIDsMatrix);
            Plugin.LogInfo($"ID Config updated to {Plugin.Instance.UserIDs.Value}");
        }
    }
}
