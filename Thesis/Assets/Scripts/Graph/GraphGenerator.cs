﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using ru.cadia.pddlFramework;

public class GraphGenerator
{

    private TreeNode<WorldState> _root;
    private HashSet<string> ids;
    private int id;
    private Graph _graph;
    private Dictionary<WorldState, string> _nodes;
    private UnityEngine.Random rnd = new UnityEngine.Random();
    private HashSet<string> _colors;
    private System.Diagnostics.Stopwatch sw;


    public GraphGenerator(TreeNode<WorldState> rootNode)
    {
        _root = rootNode;
        ids = new HashSet<string>();
        id = 0;
        _graph = null;
        _colors = null;
        sw = new System.Diagnostics.Stopwatch();
    }

    public GraphGenerator(Graph graph)
    {
        _graph = graph;
        _nodes = new Dictionary<WorldState, string>();
        _colors = new HashSet<string>();
        _root = null;
        ids = null;
        id = 0;
        sw = new System.Diagnostics.Stopwatch();
    }

    public void GenerateTree()
    {
        if (_root == null) return;
        id = 0;
        string graphml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>";
        graphml += "<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\" xmlns:java=\"http://www.yworks.com/xml/yfiles-common/1.0/java\" xmlns:sys=\"http://www.yworks.com/xml/yfiles-common/markup/primitives/2.0\" xmlns:x=\"http://www.yworks.com/xml/yfiles-common/markup/2.0\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:y=\"http://www.yworks.com/xml/graphml\" xmlns:yed=\"http://www.yworks.com/xml/yed/3\" xsi:schemaLocation=\"http://graphml.graphdrawing.org/xmlns http://www.yworks.com/xml/schema/graphml/1.1/ygraphml.xsd\">\n";
        graphml += " <graph id=\"G\" edgedefault=\"directed\">\n";
        graphml += "<node id=\"root\"/>\n";
        graphml += navigateTreeRecoursive(_root, "root");
        graphml += "</graph>\n</graphml>";

        new FileWriter().SaveFile(graphml);
    }

    private string navigateTreeRecoursive(TreeNode<WorldState> node, string parentId)
    {
        string value = "";
        foreach (TreeNode<WorldState> item in node.Children)
        {
            string parentIdLabel = "id" + id;
            if (parentId != "")
            {
                value += "<edge source=\"" + parentId + "\" target=\"" + parentIdLabel + "\"/>\n";
            }
            id++;
            if (!ids.Contains(parentIdLabel))
            {
                value += "<node id=\"" + parentIdLabel + "\"/>\n";
                ids.Add(parentIdLabel);
            }
            // Debug.Log(value);
            value += navigateTreeRecoursive(item, parentIdLabel);
        }
        return value;
    }

    public void GenerateGraphML(bool lite = false, string fileName = "")
    {
        sw.Start();
        if (_graph == null) return;
        id = 0;
        string graphml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>";
        graphml += "<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\" xmlns:java=\"http://www.yworks.com/xml/yfiles-common/1.0/java\" xmlns:sys=\"http://www.yworks.com/xml/yfiles-common/markup/primitives/2.0\" xmlns:x=\"http://www.yworks.com/xml/yfiles-common/markup/2.0\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:y=\"http://www.yworks.com/xml/graphml\" xmlns:yed=\"http://www.yworks.com/xml/yed/3\" xsi:schemaLocation=\"http://graphml.graphdrawing.org/xmlns http://www.yworks.com/xml/schema/graphml/1.1/ygraphml.xsd\">\n";
        graphml += "<key attr.name=\"url\" attr.type=\"string\" for=\"node\" id=\"d3\"/>\n";
        graphml += "<key attr.name=\"description\" attr.type=\"string\" for=\"node\" id=\"d4\"/>\n";
        graphml += "<key for=\"node\" id=\"d5\" yfiles.type=\"nodegraphics\"/>\n";
        graphml += " <key attr.name=\"url\" attr.type=\"string\" for=\"edge\" id=\"d7\"/>\n";
        graphml += "<key attr.name=\"description\" attr.type=\"string\" for=\"edge\" id=\"d8\"/>\n";
        graphml += "<key for=\"edge\" id=\"d9\" yfiles.type=\"edgegraphics\"/>\n";
        graphml += " <graph id=\"G\" edgedefault=\"directed\">\n";
        foreach (WorldState item in _graph.getAllNodeData())
        {
            string nodeName = "n" + id;
            _nodes.Add(item, nodeName);
            graphml += "<node id=\"" + nodeName + "\">\n";
            if (!lite)
            {
                graphml += "\t<data key=\"d4\" xml:space=\"preserve\"><![CDATA[" + item.ToString() + "]]></data>\n";
            }
            graphml += "\t<data key=\"d5\">\n";
            graphml += "<y:ShapeNode>\n";
            graphml += "<y:Fill color=\"#" + RandomColor() + "\" transparent=\"false\"/>";
            graphml += "<y:NodeLabel>" + nodeName + "</y:NodeLabel>\n";

            graphml += "</y:ShapeNode>\n";
            graphml += "</data>\n";
            graphml += "</node>\n";
            id++;
        }
        id = 0;
        foreach (KeyValuePair<WorldState, HashSet<WorldState>> item in _graph.getAllEdges())
        {
            string source;
            if (_nodes.TryGetValue(item.Key, out source))
            {
                foreach (WorldState ws in item.Value)
                {
                    string destination;

                    if (_nodes.TryGetValue(ws, out destination))
                    {
                        graphml += "<edge source=\"" + source + "\" target=\"" + destination + "\">\n";
                        if (!lite)
                        {
                            ru.cadia.pddlFramework.Action ac = _graph.getActionFromSourceAndDestination(item.Key, ws);
                            if (ac != null)
                            {
                                graphml += "<data key=\"d8\" xml:space=\"preserve\"><![CDATA[" + ac.ToString() + "]]></data>\n";
                                graphml += "<data key=\"d9\">\n";
                                graphml += "<y:PolyLineEdge>\n";
                                graphml += "<y:Arrows source=\"none\" target=\"standard\"/>\n";
                                // graphml += "<y:EdgeLabel>" + ac.ShortToString() + "</y:EdgeLabel>\n";
                                graphml += "</y:PolyLineEdge>\n</data>\n";
                            }
                        }
                        graphml += "</edge>\n";
                    }
                    else
                    {
                        throw new KeyNotFoundException();
                    }
                }
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }
        graphml += "</graph>\n</graphml>";
        sw.Stop();
        Debug.Log("Graph Generation time: " + (sw.ElapsedMilliseconds / 1000f));
        if (fileName != "")
            new FileWriter().SaveFile(fileName, graphml);
        else
            new FileWriter().SaveFile(graphml);
    }


    public string RandomColor()
    {
        string result = "";
        do
        {
            System.Random random = new System.Random();
            string rs = DecimalToHexadecimal(random.Next(0, 256));
            string gs = DecimalToHexadecimal(random.Next(0, 256));
            string bs = DecimalToHexadecimal(random.Next(0, 256));
            result = rs + gs + bs;
        } while (_colors.Contains(result));
        _colors.Add(result);
        // Debug.Log(result);
        return result;
    }

    private static string DecimalToHexadecimal(int dec)
    {
        if (dec <= 0)
            return "00";

        int hex = dec;
        string hexStr = string.Empty;

        while (dec > 0)
        {
            hex = dec % 16;

            if (hex < 10)
                hexStr = hexStr.Insert(0, System.Convert.ToChar(hex + 48).ToString());
            else
                hexStr = hexStr.Insert(0, System.Convert.ToChar(hex + 55).ToString());

            dec /= 16;
        }

        return hexStr;
    }

}
