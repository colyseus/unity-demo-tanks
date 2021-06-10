using System;
using System.Collections;
using System.Collections.Generic;
using Colyseus;
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

    public Transform groundPieceRoot;
    public GameObject groundPiecePrefab;

    public GameObject tankPrefab;

    public MapSchema<float> mapMatrix;
    public int MapWidth { get; private set; }
    public int MapHeight { get; private set; }
    private GameObject[,] spawnGameObjects = null;
    private GameObject[] tanks = null;

    public void BuildEnvironment(World world)
    {
        if (world.grid.Count == 0)
        {
            Debug.LogError("Received a zero width matrix!");
            return;
        }

        MapWidth = (int)world.width;
        MapHeight = (int)world.height;
        mapMatrix = world.grid;

        ClearEnvironment();
        GenerateFromMap();
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

        if (tanks != null)
        {
            for (int i = 0; i < tanks.Length; i++)
            {
                DestroyImmediate(tanks[i]);
            }
        }
    }

    #region EnvironmentUtilities

    public float GetGridValueAt(int x, int y, out int idx)
    {
        int index = x + MapWidth * y;

        idx = index;

        //LSLog.LogImportant($"Get Grid Value At ({x}, {y}) = {(float)mapMatrix.GetByIndex(index)/*mapMatrix[index]*/} - Index = {index}");
        ;
        return (float)mapMatrix[index.ToString()];
    }

    public void SetGridValueAt(int x, int y, int value)
    {
        int index = x + MapWidth * y;

        //mapMatrix.AsDictionary()[index.ToString()] = (float)value;
        mapMatrix[index.ToString()] = value;
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

        //Set our new spot to have the player
        SetGridValueAt(newCoordinates.x, newCoordinates.y, playerOne ? (int)eMapItem.PLAYER_1 : (int)eMapItem.PLAYER_2);
    }
#endregion

    //Create the map given a 1d array
    private void GenerateFromMap()
    {
        tanks = new GameObject[2];
        spawnGameObjects = new GameObject[MapWidth, MapHeight];
        int gridValue;
        for (int x = 0; x < MapWidth; ++x)
        {
            for (int y = 0; y < MapHeight; ++y)
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

                        break;
                }
            }
        }

        Vector2 temp = groundPieceRoot.localPosition;

        temp.x = TanksColyseusManager.Instance.Room.State.world.width / -2;
        groundPieceRoot.localPosition = temp;

        TankGameManager.Instance.ReportTanks(tanks[0], tanks[1]);
    }
    
    public void UpdateChangedGridCoordinate(string index, float value)
    {
        if (int.TryParse(index, out int idx))
        {
            int x = idx % MapWidth;
            int y = idx / MapWidth;

            switch ((int)value)
            {
                case (int)eMapItem.EMPTY:
                case (int)eMapItem.PLAYER_1:
                case (int)eMapItem.PLAYER_2:

                    spawnGameObjects[x, y]?.SetActive(false);

                    break;
            }
        }
        else
        {
            LSLog.LogError($"Error updating changed grid - Index = {index}  Value = {value}");
        }
    }

}
