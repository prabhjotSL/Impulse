﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SquareTileMapGenerator : MonoBehaviour
{
    #region Properties
    // Tile Prefab
    [SerializeField]
    Transform tilePrefab;

    // Tiles parent.
    [SerializeField]
    Transform tilesParent;

    // Obstacle Prefab
    [SerializeField]
    Transform obstaclePrefab;

    // Obstacle parent
    [SerializeField]
    Transform obstaclesParent;

    // Navmesh Floor
    [SerializeField]
    Transform navmeshFloor;

    // Navmesh Floor Mask.
    // Prevents areas of the navmesh floor not covered by tiles to be marked as walkable.
    [SerializeField]
    Transform navmeshMaskPrefab;

    // Navmesh Floor Masks parent.
    [SerializeField]
    Transform navmeshMasksParent;

    // Map parent
    [SerializeField]
    Transform mapParent;

    // Settings
    [SerializeField]
    Vector2 minMapSize;
    public Vector2 MinMapSize
    {
        get { return minMapSize; }
    }

    [SerializeField]
    Vector2 maxMapSize;

    [SerializeField]
    [Range(0, 1)]
    float obstaclePercent;
    public float ObstaclePercent
    {
        get { return obstaclePercent; }
    }

    [SerializeField]
    int obstaclePlaceSeed;
    public int ObstaclePlaceSeed
    {
        get { return obstaclePlaceSeed; }
    }

    [SerializeField]
    float minObstacleHeight;
    public float MinObstacleHeight
    {
        get { return minObstacleHeight; }
    }

    [SerializeField]
    float maxObstacleHeight;
    public float MaxObstacleHeight
    {
        get { return maxObstacleHeight; }
    }

    [SerializeField]
    Color foregroundColor;
    public Color ForegroundColor
    {
        get { return foregroundColor; }
    }

    [SerializeField]
    Color backgroundColor;
    public Color BackgroundColor
    {
        get { return backgroundColor; }
    }

    [SerializeField]
    float tileSize = 1f;

    [SerializeField]
    [Range(0, 1)]
    float tileOutlinePercent;
    public float TileOutlinePercent
    {
        get { return tileOutlinePercent; }
    }

    // Tiles list.
    List<SquareTile> tiles = new List<SquareTile>();

    // Coordinates list.
    List<Point> points;
    Queue<Point> shuffledTilePoints;

    Point mapCenter
    {
        get { return new Point((int)minMapSize.x / 2, (int)minMapSize.y / 2); }
    }

    #endregion

    public void GenerateMap()
    {
        // Set floor box collider size.
        navmeshFloor.GetComponent<BoxCollider>().size = new Vector3(minMapSize.x * tileSize, 0.05f, minMapSize.y * tileSize);

        // Random number seed for placing obstacles and modifying obstacle height.
        System.Random pRNG = new System.Random(obstaclePlaceSeed);

        points = new List<Point>();
        for (int x = 0; x < minMapSize.x; x++)
        {
            for (int y = 0; y < minMapSize.y; y++)
            {
                // Add the associated point to the points array.
                points.Add(new Point(x, y));
            }
        }
        shuffledTilePoints = new Queue<Point>(SquareTileMapUtility.ShuffleArray(points.ToArray(), obstaclePlaceSeed));

        // Destroy previous tiles.
        if (tiles != null)
        {
            for (int i = tilesParent.childCount - 1; i >= 0; --i)
            {
                DestroyImmediate(tilesParent.GetChild(i).gameObject);
            }
            tiles.Clear();
        }

        // Destroy previous obstacles.
        for (int i = obstaclesParent.childCount - 1; i >= 0; --i)
        {
            DestroyImmediate(obstaclesParent.GetChild(i).gameObject);
        }

        // Loop through axes to generate map.
        for (int x = 0; x < minMapSize.x; x++)
        {
            for (int y = 0; y < minMapSize.y; y++)
            {
                Vector3 tilePosition = PointToPosition(x, y);
                Transform newTile = Instantiate(tilePrefab, tilePosition, Quaternion.Euler(Vector3.right * 90)) as Transform;
                newTile.localScale = Vector3.one * (1 - tileOutlinePercent) * tileSize;
                newTile.transform.parent = tilesParent;

                if (newTile.GetComponent<SquareTile>() == null)
                    newTile.gameObject.AddComponent<SquareTile>();

                SquareTile newSquareTile = newTile.gameObject.GetComponent<SquareTile>();
                newSquareTile.SetPos(x, y);
                newSquareTile.Height = 0;
                tiles.Add(newSquareTile);
            }
        }

        // Used in ensuring map connectivity.
        bool[,] obstacleMap = new bool[(int)minMapSize.x, (int)minMapSize.y];
        int obstacleFillAmount = (int)(minMapSize.x * minMapSize.y * obstaclePercent);
        int obstacleCount = 0;

        // Generate obstacles.
        for (int i = 0; i < obstacleFillAmount; i++)
        {
            Point randomPoint = GetRandomPoint();

            // We presume we are instantiating an obstacle at the point before we can check if the obstacle closes anything off.
            obstacleMap[randomPoint.x, randomPoint.y] = true;
            obstacleCount++;

            // We never want to spawn an obstacle in the center of the map or in a place that would cut off a path.
            if (randomPoint != mapCenter && MapIsFullyAccessible(obstacleMap, obstacleCount))
            {
                float obstacleHeight = Mathf.Lerp(minObstacleHeight, maxObstacleHeight, (float)pRNG.NextDouble());
                Vector3 obstaclePosition = PointToPosition(randomPoint.x, randomPoint.y);
                Transform newObstacle = Instantiate(obstaclePrefab, obstaclePosition + (Vector3.up * obstacleHeight/2f), Quaternion.identity) as Transform;
                newObstacle.localScale = new Vector3((1 - tileOutlinePercent) * tileSize, obstacleHeight, (1 - tileOutlinePercent) * tileSize);
                newObstacle.parent = obstaclesParent;

                // Color settings based on foreground/background colors.
                Renderer obstacleRenderer = newObstacle.GetComponent<Renderer>();
                Material obstacleMaterial = new Material(obstacleRenderer.sharedMaterial);

                // Determine how far 'forward' this obstacle is.
                float colorPercent = randomPoint.y / (float)minMapSize.y;

                obstacleMaterial.color = Color.Lerp(foregroundColor, backgroundColor, colorPercent);
                obstacleRenderer.sharedMaterial = obstacleMaterial;
            }
            else
            {
                // If the check fails, then we remove the obstacle location from the obstacle map.
                obstacleMap[randomPoint.x, randomPoint.y] = false;
                obstacleCount--;
            }
        }

        // Destroy previous navmesh floor masks.
        for (int i = navmeshMasksParent.childCount - 1; i >= 0; --i)
        {
            DestroyImmediate(navmeshMasksParent.GetChild(i).gameObject);
        }

        // Generate navmesh floor masks.
        Transform maskLeft = Instantiate(navmeshMaskPrefab, Vector3.left * (minMapSize.x + maxMapSize.x) / 4f * tileSize, Quaternion.identity) as Transform;
        maskLeft.parent = navmeshMasksParent;
        Transform maskRight = Instantiate(navmeshMaskPrefab, Vector3.right * (minMapSize.x + maxMapSize.x) / 4f * tileSize, Quaternion.identity) as Transform;
        maskRight.parent = navmeshMasksParent;
        Transform maskTop = Instantiate(navmeshMaskPrefab, Vector3.forward * (minMapSize.y + maxMapSize.y) / 4f * tileSize, Quaternion.identity) as Transform;
        maskTop.parent = navmeshMasksParent;
        Transform maskBottom = Instantiate(navmeshMaskPrefab, Vector3.back * (minMapSize.y + maxMapSize.y) / 4f * tileSize, Quaternion.identity) as Transform;
        maskBottom.parent = navmeshMasksParent;

        maskLeft.localScale = new Vector3((maxMapSize.x - minMapSize.x) / 2f, 1, minMapSize.y) * tileSize;
        maskRight.localScale = new Vector3((maxMapSize.x - minMapSize.x) / 2f, 1, minMapSize.y) * tileSize;
        maskTop.localScale = new Vector3(maxMapSize.x, 1, (maxMapSize.y - minMapSize.y) / 2f) * tileSize;
        maskBottom.localScale = new Vector3(maxMapSize.x, 1, (maxMapSize.y - minMapSize.y) / 2f) * tileSize;

        navmeshFloor.localScale = new Vector3(maxMapSize.x, maxMapSize.y) * tileSize;

        // Save to map parent.
        if (mapParent.GetComponent<SquareTileMap>() == null)
            mapParent.gameObject.AddComponent<SquareTileMap>();

        mapParent.GetComponent<SquareTileMap>().Setup(minMapSize, obstaclePercent, obstaclePlaceSeed, minObstacleHeight, maxObstacleHeight, foregroundColor, backgroundColor);

    }

    // Uses a flood-fill algorithm to check for map connectivity.
    bool MapIsFullyAccessible (bool[,] obstacleMap, int currentObstacleCount)
    {
        bool[,] mapFlags = new bool[obstacleMap.GetLength(0), obstacleMap.GetLength(1)];
        Queue<Point> queue = new Queue<Point>();

        // The center of the map should never have an obstacle, so preemptively set its location to true.
        // Note that this can be changed to any arbitrary point.
        queue.Enqueue(mapCenter);
        mapFlags[mapCenter.x, mapCenter.y] = true;
        int accessibleTileCount = 1;

        while (queue.Count > 0)
        {
            Point tile = queue.Dequeue();

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    // Loop through and check each adjacent tile.
                    int neighborX = tile.x + x;
                    int neighborY = tile.y + y;

                    // Since we only care about cardinal adjacent tiles, exclude diagonal neighbors.
                    if (x == 0 || y == 0)
                    {
                        // Ensure the adjacent tile we are checking is actually a point in the obstacle map.
                        if (neighborX >= 0 && neighborX < obstacleMap.GetLength(0) && neighborY >= 0 && neighborY < obstacleMap.GetLength(1))
                        {
                            // Ignore this spot if we have checked this tile previously or if it is already marked as an obstacle's location.
                            if (!mapFlags[neighborX, neighborY] && !obstacleMap[neighborX, neighborY])
                            {
                                mapFlags[neighborX, neighborY] = true;
                                queue.Enqueue(new Point(neighborX, neighborY));
                                accessibleTileCount++;
                            }
                        }
                    }
                }
            }
        }

        int targetAccessibleTilecount = (int)(minMapSize.x * minMapSize.y - currentObstacleCount);
        return targetAccessibleTilecount == accessibleTileCount;
    }


    Point GetRandomPoint()
    {
        Point randomPoint = shuffledTilePoints.Dequeue();
        shuffledTilePoints.Enqueue(randomPoint);
        
        return randomPoint;
    }

    Vector3 PointToPosition(int x, int y)
    {
        return new Vector3(-minMapSize.x / 2f + 0.5f + x, 0, -minMapSize.y / 2f + 0.5f + y) * tileSize;
    }

    void Start()
    {
        if (tilesParent == null)
            tilesParent = transform;
    }
}
