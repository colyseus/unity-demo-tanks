using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRules
{
    public static int MovementAPCost => ExampleManager.Instance.Room.State.gameRules.MovementActionPointCost;
    public static int FiringAPCost => ExampleManager.Instance.Room.State.gameRules.FiringActionPointCost;
    public static int MaxAP => ExampleManager.Instance.Room.State.gameRules.MaxActionPoints;
}
