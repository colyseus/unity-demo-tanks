using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Colyseus
{

    //Will add info here as we need it (current turn, for example)
    [System.Serializable]
    public class TanksRoomMetadata
    {
        public string team0;
        public string team1;

        public bool IsUserCreator(string userName)
        {
            return userName.Equals(team0);
        }
    }
}