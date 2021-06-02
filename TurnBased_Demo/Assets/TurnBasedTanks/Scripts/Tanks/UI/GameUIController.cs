using System;
using System.Collections;
using System.Collections.Generic;
using LucidSightTools;
using UnityEngine;
using TMPro;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    public bool MenuOpen
    {
        get { return escapeMenu.activeInHierarchy; }
    }

    [SerializeField]
    private TextMeshProUGUI mapDisplay;

    [SerializeField]
    private RectTransform mapTransform;

    [SerializeField]
    private Image[] actionPointIcons;
    
    [SerializeField]
    private TextMeshProUGUI activeWeaponName;

    [SerializeField]
    private TextMeshProUGUI currentPlayer;

    [SerializeField]
    private TextMeshProUGUI yourName;

    [SerializeField]
    private Image[] yourHPIcons;

    [SerializeField]
    private TextMeshProUGUI enemyName;

    [SerializeField]
    private Image[] enemyHPIcons;

    [SerializeField]
    private TextMeshProUGUI winLossMessage;

    [SerializeField]
    private TextMeshProUGUI generalMessage;

    [SerializeField]
    private Button rematchButton;

    [SerializeField]
    private Button exitButton;

    [SerializeField]
    private TextMeshProUGUI quitButtonText;

    [SerializeField]
    private GameObject gameOverMenu;

    [SerializeField]
    private GameObject escapeMenu;

    [SerializeField]
    private Image onlineIndicator;

    [SerializeField]
    private Image loadingCover;

    private bool mapShowing;

    void Awake()
    {
        UpdateMapVisibility();
    }

    void Start()
    {
        escapeMenu.SetActive(false);
    }

    private void Update()
    {
        if (generalMessage.gameObject.activeInHierarchy)
        {
            generalMessage.text = TankGameManager.Instance.GeneralMessage;
        }

        if (TankGameManager.Instance.IsGameOver == false && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleEscapeMenu();
        }
    }

    public void ToggleLoadingCover(bool show)
    {
        loadingCover.gameObject.SetActive(show);
    }

    public void UpdateMap(EnvironmentBuilder environment)
    {
        string text = "";
        int gridValue;
        for (int y = environment.MapHeight - 1/*map[0].Count - 1*/; y >= 0; --y)
        {
            for (int x = 0; x < environment.MapWidth/*map.Count*/; ++x)
            {
                gridValue = (int)environment.GetGridValueAt(x, y, out int idx);
                string item = gridValue.ToString(); /*map[x][y].ToString();*/
                switch (gridValue)
                {
                    case (int)EnvironmentBuilder.eMapItem.EMPTY:
                        item = $"<color=#75B8FF>X</color>";
                        break;
                    case (int)EnvironmentBuilder.eMapItem.GROUND:
                        item = $"<color=#4B2F00>X</color>";
                        break;
                    case (int)EnvironmentBuilder.eMapItem.PLAYER_1:
                        item = $"<color=blue>X</color>";
                        break;
                    case (int)EnvironmentBuilder.eMapItem.PLAYER_2:
                        item = $"<color=red>X</color>";
                        break;
                }
                text += item;
            }

            text += "\n";
        }

        mapDisplay.text = text;
    }

    public void UpdatePlayerNames(string ourPlayerName, string challengerName)
    {
        yourName.text = string.IsNullOrEmpty(ourPlayerName) ? "Your Tank" : ourPlayerName;
        enemyName.text = string.IsNullOrEmpty(challengerName) ? "Enemy Tank" : challengerName;
    }

    public void SetCurrentPlayer(string playerName)
    {
        currentPlayer.text = $"{(string.IsNullOrEmpty(playerName) ? "Enemy Tank" : playerName)}'s Turn";
    }

    public void UpdateActionPoints(int current)
    {
        for (int i = 0; i < actionPointIcons.Length; ++i)
        {
            actionPointIcons[i].enabled = i < current;
        }
    }

    public void UpdateHitPoints(int val, bool isOurs)
    {
        if (isOurs)
        {
            UpdateHitPoints(yourHPIcons, val);
        }
        else
        {
            UpdateHitPoints(enemyHPIcons, val);
        }
    }

    private void UpdateHitPoints(Image[] icons, int val)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].enabled = i < val;
        }
    }

    private void ToggleEscapeMenu()
    {
        escapeMenu.SetActive(!escapeMenu.activeInHierarchy);

        quitButtonText.text = TankGameManager.Instance.IsGameOver ? "Quit" : "Surrender";
    }

    public void ShowGameOverUI(int winnerId, string quittingMsg = "")
    {
        gameOverMenu.SetActive(true);
        generalMessage.gameObject.SetActive(true);
        rematchButton.gameObject.SetActive(true);
        rematchButton.interactable = string.IsNullOrEmpty(quittingMsg);

        winLossMessage.text = winnerId == TankGameManager.Instance.OurPlayerID ? "Winner!" : "Loss...";

        if (string.IsNullOrEmpty(quittingMsg) == false)
        {
            winLossMessage.text += $"\n{quittingMsg}";
        }

        generalMessage.text = "";

    }

    public void HideGameOverUI()
    {
        gameOverMenu.SetActive(false);
    }

    public void OnButtonEvent_RequestRematch()
    {
        winLossMessage.text += "\nWaiting for other player...";
        generalMessage.gameObject.SetActive(false);
        rematchButton.gameObject.SetActive(false);

        TankGameManager.Instance.RequestRematch();
    }

    public void OnButtonEvent_QuitMatch()
    {
        TankGameManager.Instance.QuitMatch();
    }

    public void OnButtonEvent_ExitToLobby()
    {
        TankGameManager.Instance.ReturnToLobby();
    }

    public void ToggleMap()
    {
        mapShowing = !mapShowing;
        UpdateMapVisibility();
    }

    private void UpdateMapVisibility()
    {
        mapTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, mapShowing ? 150 : 0);
        mapDisplay.gameObject.SetActive(mapShowing);
    }

    public void SetActiveWeaponName(string name)
    {
        activeWeaponName.text = $"{(string.IsNullOrEmpty(name) ? "" : "Current Weapon - ")}{name}";

        activeWeaponName.gameObject.SetActive(!string.IsNullOrEmpty(name));
    }

    public void ToggleOnlineIndicator(bool active)
    {
        onlineIndicator.color = active ? Color.green : Color.red;
    }
}
