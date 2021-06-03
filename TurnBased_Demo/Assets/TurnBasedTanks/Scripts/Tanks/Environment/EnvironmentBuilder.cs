using System;
using System.Collections;
using System.Collections.Generic;
using Colyseus.Schema;
using GameDevWare.Serialization;
using LucidSightTools;
using Tanks;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

public class EnvironmentBuilder : MonoBehaviour
{
    public enum eMapItem
    {
        EMPTY = 0,
        GROUND = 1,
        PLAYER_1 = 2,
        PLAYER_2 = 3
    }

    public class MapCoordinates
    {
        public int x;
        public int y;

        public MapCoordinates(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public Transform leftBounds;
    public Transform rightBounds;
    public Transform bottomBounds;
    
    public Transform groundPieceRoot;
    public GameObject groundPiecePrefab;

    public GameObject tankPrefab;

    public ArraySchema<float> mapMatrix;
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    private GameObject[,] spawnGameObjects = null;

    public void BuildEnvironment(World world)
    {
        if (world.grid.Count == 0)
        {
            Debug.LogError("Received a zero width matrix!");
            return;
        }

        MapWidth = (int)world.width/*matrix.Count*/;
        MapHeight = (int)world.height/*matrix[0].Count*/;
        mapMatrix = world.grid;

        //for (int i = 0; i < world.grid.Count; i++)
        //{
        //    mapMatrix.Add((int)world.grid[i]);
        //}

        //for (int x = 0; x < MapWidth; ++x)
        //{
        //    for (int y = 0; y < MapHeight; ++y)
        //    {
        //        GetGridValueAt(x, y);
        //    }
        //}

        //LSLog.LogImportant($"*** World Grid To String - {world.grid.Count}***", LSLog.LogColor.cyan);
        //string arrayString = "[";
        //for (int i = 0; i < world.grid.Count; i++)
        //{
        //    arrayString += mapMatrix[i].ToString();

        //    if (i < world.grid.Count - 1)
        //        arrayString += ",";
        //}

        //arrayString += "]";
        //Debug.Log(arrayString);

        ClearEnvironment();
        GenerateFromMap(mapMatrix);
    }

    public void ClearEnvironment()
    {
        if (spawnGameObjects != null)
        {
            for (int x = 0; x < spawnGameObjects.GetLength(0); ++x)
            {
                for (int y = 0; y < spawnGameObjects.GetLength(1); ++y)
                {
                    DestroyImmediate(spawnGameObjects[x,y]);
                }
            }

            spawnGameObjects = null;
        }
    }

    #region EnvironmentUtilities

    public float GetGridValueAt(int x, int y, out int idx)
    {
        int index = x + MapWidth * y;

        idx = index;

        //LSLog.LogImportant($"Get Grid Value At ({x}, {y}) = {(float)mapMatrix.GetByIndex(index)/*mapMatrix[index]*/} - Index = {index}");

        return (float)mapMatrix.GetByIndex(index); //mapMatrix[index];
    }

    public void SetGridValueAt(int x, int y, int value)
    {
        int index = x + MapWidth * y;
        mapMatrix[index] = value;
    }

    public Vector3 CoordinateToWorldPosition(MapCoordinates coordinates)
    {
        return groundPieceRoot.TransformPoint(new Vector3(coordinates.x, coordinates.y, 0));
    }

    public MapCoordinates WorldPositionToMapCoordinates(Vector3 worldPos)
    {
        Vector3 localPos = groundPieceRoot.InverseTransformPoint(worldPos);
        localPos.y = Mathf.Clamp(localPos.y, 0.0f, float.MaxValue);
        MapCoordinates coords = new MapCoordinates((int)Mathf.RoundToInt( localPos.x), (int)Mathf.RoundToInt(localPos.y));
        if (coords.y >= MapHeight)
        {
            return null;    //We're shooting over the top
        }

        if (coords.x < 0 || coords.x >= MapWidth)
        {
            return null;
        }
        return coords;
    }

    public CannonController.CannonFirePath TrimFirePathToEnvironment(CannonController.CannonFirePath origPath)
    {
        List<Vector3> updatedPath = new List<Vector3>();
        updatedPath.Add(origPath.path[0]);
        bool foundTermination = false;
        for (int i = 0; i < origPath.path.Length; ++i)
        {
            if(foundTermination)
                break;
            
            MapCoordinates coords = WorldPositionToMapCoordinates(origPath.path[i]);
            if (coords == null)
            {
                //We're shooting over the map, keep the position
                updatedPath.Add(origPath.path[i]);
            }
            else
            {
                try
                {
                    //Check to see what is here
                    switch (GetGridValueAt(coords.x, coords.y, out int idx)/*mapMatrix[coords.x][coords.y]*/)
                    {
                        case (int)eMapItem.EMPTY:
                            updatedPath.Add(origPath.path[i]);
                            break;
                        case (int)eMapItem.GROUND:
                            foundTermination = true;
                            //Create a new position thats 0.5 up from the ground block
                            Vector3 pos = CoordinateToWorldPosition(coords);
                            pos.x = origPath.path[i].x;
                            pos.y += 0.5f;
                            updatedPath.Add(pos);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error getting map coordinate: " + coords.x + ", " + coords.y);
                    throw;
                }
                
            }
        }

        return new CannonController.CannonFirePath(updatedPath.ToArray());
    }
    
    public void MovePlayer(bool playerOne, MapCoordinates previousCoordinates, MapCoordinates newCoordinates)
    {
        //Set our old spot to empty
        SetGridValueAt(previousCoordinates.x, previousCoordinates.y, (int)eMapItem.EMPTY);
        //mapMatrix[previousCoordinates.x][previousCoordinates.y] = (int)eMapItem.EMPTY;
        //Set our new spot to have the player
        //mapMatrix[newCoordinates.x][newCoordinates.y] = playerOne ? (int)eMapItem.PLAYER_1 : (int)eMapItem.PLAYER_2;
        SetGridValueAt(newCoordinates.x, newCoordinates.y, playerOne ? (int)eMapItem.PLAYER_1 : (int)eMapItem.PLAYER_2);
    }
#endregion

    //Create the map given a 1d array
    private void GenerateFromMap(ArraySchema<float> map)
    {
        GameObject[] tanks = new GameObject[2];
        spawnGameObjects = new GameObject[MapWidth, MapHeight];
        int gridValue;
        for (int x = 0; x < MapWidth/*map.Count*/; ++x)
        {
            for (int y = 0; y < MapHeight/*map[x].Count*/; ++y)
            {
                gridValue = (int)GetGridValueAt(x, y, out int idx);
                switch (gridValue)
                {
                    case (int)eMapItem.GROUND:
                        GameObject piece = Instantiate(groundPiecePrefab, groundPieceRoot, false);
                        piece.name = $"({x}, {y}) - {idx}";
                        piece.transform.localPosition = new Vector3(x, y, 0);
                        spawnGameObjects[x, y] = piece;
                        break;
                    case (int)eMapItem.PLAYER_1:
                    case (int)eMapItem.PLAYER_2:
                        GameObject tank = Instantiate(tankPrefab, groundPieceRoot, false);
                        tank.transform.localPosition = new Vector3(x, y, 0);

                        tank.GetComponent<TankController>().SetCoordinates(x, y);

                        if (gridValue == (int)eMapItem.PLAYER_2)
                        {
                            tank.transform.localRotation = Quaternion.Euler(0, 180, 0);
                            tanks[1] = tank;
                        }
                        else
                        {
                            tanks[0] = tank;
                        }
                        spawnGameObjects[x, y] = tank;
                        break;
                }
            }
        }

        Vector2 temp = groundPieceRoot.localPosition;

        temp.x = /*map.Count*/ ExampleManager.Instance.Room.State.world.width / -2;
        groundPieceRoot.localPosition = temp;

        TankGameManager.Instance.ReportTanks(tanks[0], tanks[1]);
    }
    
    public void DamageDealt(DamageData damageData)
    {
        //LSLog.LogImportant($"Environment - Damage Dealt - Impacted coordinates");

        if (damageData == null || damageData.impactedCoordinates == null)
        {
            return;
        }

        for (int i = 0; i < damageData.impactedCoordinates.Count; i++)
        {
            Vector2 coords = damageData.impactedCoordinates[i];
            //LSLog.LogImportant($"\tCoord = ({coords.x}, {coords.y})");
            int segment = (int)GetGridValueAt((int)coords.x, (int)coords.y, out int idx);// mapMatrix[(int) coords.x][(int) coords.y];

            switch (segment)
            {
                case (int)eMapItem.GROUND:
                    //Ground goes boom?
                    SetGridValueAt((int)coords.x, (int)coords.y, (int)eMapItem.EMPTY);
                    //mapMatrix[(int)coords.x][(int)coords.y] = (int)eMapItem.EMPTY;
                    spawnGameObjects[(int)coords.x, (int)coords.y]?.SetActive(false);
                    break;
            }
            
        }

    }

}
