using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRules
{
    public static int MovementAPCost => TanksColyseusManager.Instance.Room.State.gameRules.MovementActionPointCost;
    public static int FiringAPCost => TanksColyseusManager.Instance.Room.State.gameRules.FiringActionPointCost;
    public static int MaxAP => TanksColyseusManager.Instance.Room.State.gameRules.MaxActionPoints;
}
