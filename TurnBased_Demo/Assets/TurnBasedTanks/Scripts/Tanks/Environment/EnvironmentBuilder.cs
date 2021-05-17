using System;
using System.Collections;
using System.Collections.Generic;
using LucidSightTools;
using UnityEngine;
using Random = UnityEngine.Random;

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

    public List<List<int>> mapMatrix;
    private int mapWidth;
    private int mapHeight;
    private GameObject[,] spawnGameObjects = null;

    public void BuildEnvironment(List<List<int>> matrix)
    {
        if (matrix.Count == 0)
        {
            Debug.LogError("Received a zero width matrix!");;
            return;
        }

        mapWidth = matrix.Count;
        mapHeight = matrix[0].Count;
        mapMatrix = matrix;

        ClearEnvironment();
        GenerateFromMap(matrix);
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
    public Vector3 CoordinateToWorldPosition(MapCoordinates coordinates)
    {
        return groundPieceRoot.TransformPoint(new Vector3(coordinates.x, coordinates.y, 0));
    }

    public MapCoordinates WorldPositionToMapCoordinates(Vector3 worldPos)
    {
        Vector3 localPos = groundPieceRoot.InverseTransformPoint(worldPos);
        localPos.y = Mathf.Clamp(localPos.y, 0.0f, float.MaxValue);
        MapCoordinates coords = new MapCoordinates((int)Mathf.RoundToInt( localPos.x), (int)Mathf.RoundToInt(localPos.y));
        if (coords.y >= mapHeight)
        {
            return null;    //We're shooting over the top
        }

        if (coords.x < 0 || coords.x >= mapWidth)
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
                    switch (mapMatrix[coords.x][coords.y])
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
        mapMatrix[previousCoordinates.x][previousCoordinates.y] = (int)eMapItem.EMPTY;
        //Set our new spot to have the player
        mapMatrix[newCoordinates.x][newCoordinates.y] = playerOne ? (int)eMapItem.PLAYER_1 : (int)eMapItem.PLAYER_2;
    }
#endregion

    //Create the map given a 2d array
    private void GenerateFromMap(List<List<int>> map)
    {
        GameObject[] tanks = new GameObject[2];
        spawnGameObjects = new GameObject[mapWidth, mapHeight];
        for (int x = 0; x < map.Count; ++x)
        {
            for (int y = 0; y < map[x].Count; ++y)
            {
                if (map[x][y] == (int)eMapItem.GROUND)
                {
                    GameObject piece = Instantiate(groundPiecePrefab, groundPieceRoot, false);
                    piece.transform.localPosition = new Vector3(x, y, 0);
                    spawnGameObjects[x,y] = piece;
                }
                else if (map[x][y] == (int)eMapItem.PLAYER_1 || map[x][y] == (int)eMapItem.PLAYER_2)
                {
                    GameObject tank = Instantiate(tankPrefab, groundPieceRoot, false);
                    tank.transform.localPosition = new Vector3(x, y, 0);

                    tank.GetComponent<TankController>().SetCoordinates(x, y);

                    if (map[x][y] == (int)eMapItem.PLAYER_2)
                    {
                        tank.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        tanks[1] = tank;
                    }
                    else
                    {
                        tanks[0] = tank;
                    }
                    spawnGameObjects[x, y] = tank;
                }
            }
        }

        Vector2 temp = groundPieceRoot.localPosition;

        temp.x = map.Count / -2;
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
            int segment = mapMatrix[(int) coords.x][(int) coords.y];

            switch (segment)
            {
                case (int)eMapItem.GROUND:
                    //Ground goes boom?
                    mapMatrix[(int)coords.x][(int)coords.y] = (int)eMapItem.EMPTY;
                    spawnGameObjects[(int)coords.x, (int)coords.y]?.SetActive(false);
                    break;
            }
            
        }

    }

}
