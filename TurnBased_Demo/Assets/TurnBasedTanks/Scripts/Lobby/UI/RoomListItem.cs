using Colyseus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI roomStatus = null;

    [SerializeField]
    private TextMeshProUGUI additionalInfo = null;

    [SerializeField]
    private Button joinButton = null;

    private RoomSelectionMenu menuRef;

    [SerializeField]
    private TextMeshProUGUI roomName = null;

    private TanksRoomsAvailable roomRef;

    public void Initialize(ColyseusRoomAvailable roomReference, RoomSelectionMenu menu)
    {
        menuRef = menu;
        roomRef = roomReference as TanksRoomsAvailable;
        roomName.text = roomReference.roomId;
        string maxClients = roomReference.maxClients > 0 ? roomReference.maxClients.ToString() : "--";
        SetRoomStatus();
    }

    private void SetRoomStatus()
    {
        //joinButton.interactable = false;
        additionalInfo.text = "";
        TanksRoomMetadata metaData = roomRef.metadata;
        if (metaData.team0 != null && metaData.team1 != null)
        {
            //Full game, confirm we're one of these two teams
            roomStatus.text = "GAME IN PROGRESS";
            additionalInfo.text = $"{metaData.team0} vs. {metaData.team1}";
            joinButton.interactable = true;
        }
        else if (metaData.team0 != null && metaData.team1 == null)
        {
            //Someone has made this game but they're waiting for a second player
            if (metaData.IsUserCreator(ExampleManager.Instance.UserName))
            {
                roomStatus.text = "AWAITING CHALLENGER";
                joinButton.interactable = true;
            }
            else
            {
                roomStatus.text = "CHALLENGER WANTED";
                joinButton.interactable = true;
            }
        }
        else
        {
            //Shouldn't happen?
            Debug.LogError("Game has unsupported metadata!");
        }
    }

    public void TryJoin()
    {
        if ((roomRef.metadata.team1 != null &&ExampleManager.Instance.UserName.Equals(roomRef.metadata.team1)) || ExampleManager.Instance.UserName.Equals(roomRef.metadata.team0))
        {
            //RE-Join a room
            menuRef.RejoinRoom(roomRef.roomId);
        }
        else
        {
            menuRef.JoinRoom(roomRef.roomId);
        }
    }
}