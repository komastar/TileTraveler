﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class Map : MonoBehaviour
{
    public NodeObject nodePrefab;
    public MapModel mapData;
    public Vector2Int mapSize;
    public float nodeSize;
    public string mapName;

    public void Clear()
    {
        var children = GetComponentsInChildren<NodeObject>();
        for (int i = 0; i < children.Length; i++)
        {
            DestroyImmediate(children[i].gameObject);
        }
    }

    public void Save()
    {
        string saveName = mapData.Name;
        if (string.IsNullOrEmpty(saveName))
        {
            saveName = mapName;
            mapData.NodeSize = 1f;
            mapData.MapSize = new GridInt(mapSize.x, mapSize.y);
        }

        var nodes = GetComponentsInChildren<NodeObject>();
        mapData.Nodes = new NodeModel[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            mapData.Nodes[i].Convert(nodes[i]);
        }

        var save = JObject.FromObject(mapData).ToString(Formatting.Indented);
        string path = $"{Application.dataPath}/Data/.Map";
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        path = $"{path}/{saveName}.json";

        File.WriteAllText(path, save);
    }

    public void Generate()
    {
        mapSize.x = mapData.MapSize.x;
        mapSize.y = mapData.MapSize.y;
        mapName = mapData.Name;
        nodeSize = mapData.NodeSize;

        float offsetX = (nodeSize * 0.5f) * (mapData.MapSize.x - 1);
        float offsetY = (nodeSize * 0.5f) * (mapData.MapSize.y - 1);
        for (int y = 0; y < mapData.MapSize.y; y++)
        {
            for (int x = 0; x < mapData.MapSize.x; x++)
            {
                var newNode = Instantiate(nodePrefab, transform);
                newNode.name = "EmptyNode";
                newNode.transform.localPosition = new Vector3(x * nodeSize - offsetX, y * nodeSize - offsetY, 0);
                newNode.Position = new Vector2Int(x, y);
            }
        }

        Camera.main.orthographicSize = Math.Max(mapSize.x * nodeSize, mapSize.y * nodeSize);
    }

    public void Load(Dictionary<int, RouteModel> routeData, Dictionary<string, Sprite> spriteData)
    {
        mapSize.x = mapData.MapSize.x;
        mapSize.y = mapData.MapSize.y;
        mapName = mapData.Name;
        nodeSize = mapData.NodeSize;

        float offsetX = (nodeSize * 0.5f) * (mapData.MapSize.x - 1);
        float offsetY = (nodeSize * 0.5f) * (mapData.MapSize.y - 1);

        var nodes = mapData.Nodes;
        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            var newNode = Instantiate(nodePrefab, transform);
            newNode.name = "EmptyNode";
            if (node.Id != 0)
            {
                newNode.SetupNode(node.Id, spriteData[routeData[node.Id].Name]);
            }
            else
            {
                newNode.SetupNode(node.Id, null);
            }
            newNode.transform.localPosition = new Vector3(node.Position.x - offsetX, node.Position.y - offsetY, 0f);
            newNode.Position = node.Position.ToVector2Int();
            newNode.Rotate((int)node.Direction);
        }

        Camera.main.orthographicSize = Math.Max(mapSize.x * nodeSize, mapSize.y * nodeSize);
    }

    public void Reset()
    {
        var selected = GetAllSelected();
        if (selected.Count > 0)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].ResetNode();
            }
        }
    }

    public void SetRoute(int id, Sprite sprite)
    {
        var selected = GetAllSelected();
        if (selected.Count > 0)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].SetupNode(id, sprite);
            }
        }
    }

    public void Rotate()
    {
        var selected = GetAllSelected();
        if (selected.Count > 0)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].Rotate();
            }
        }
    }

    private List<NodeObject> GetAllSelected()
    {
        var objs = Selection.objects;
        List<NodeObject> nodes = new List<NodeObject>();
        for (int i = 0; i < objs.Length; i++)
        {
            nodes.Add((objs[i] as GameObject).GetComponent<NodeObject>());
        }

        return nodes;
    }
}