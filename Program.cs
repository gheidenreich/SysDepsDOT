using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Builder {
	class Program {
		static string inTxt = "";
		//static string outTxt = "";
		static void Main(string[] args) {
			DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
			string inTxtPath = Path.Combine(Environment.CurrentDirectory, "in.txt");
			if (Environment.GetCommandLineArgs().Length > 0)
				inTxtPath = Path.Combine(Environment.CurrentDirectory, Environment.GetCommandLineArgs()[1]);
			
			string outTxtPath = Path.Combine(Environment.CurrentDirectory, "out.txt");

			inTxt = File.ReadAllText(inTxtPath);
			Builder b = new Builder();
			string result = b.Process(inTxt);
			File.WriteAllText(outTxtPath, result);
			Console.WriteLine(result);
			Console.WriteLine();
			Console.WriteLine("Press key to close...");
			//Console.ReadLine();
		}
	}

	class Builder {
		string start = "digraph g {";
		string end = "}";
		List<string> setup = new List<string>();
		List<string> nodeLines = new List<string>();
		List<string> edgeLines = new List<string>();

		public string Process(string inTxt) {
			BuildSetup();
			List<string> inLines = new List<string>(inTxt.Split('\n'));

			//clean comments & blank lines
			List<string> newInLines = new List<string>();
			foreach (string ln in inLines) {
				string t = ln;
				if (ln.Contains("#"))
					t = ln.Substring(0, ln.IndexOf("#"));
				if (string.IsNullOrWhiteSpace(t))
					continue;
				newInLines.Add(t);
			}
			inLines = newInLines;

			List<Node> nodes = new List<Node>();
			List<Edge> edges = new List<Edge>();

			//////////////////////////////////////////////////////////////////////////////////////////
			//gather nodes
			int id = 0;
			foreach (string ln in inLines) {
				if (char.IsLetter(ln[0])) //as opposed to a tab
					nodes.Add(new Node(ln.Trim(), "node" + id++.ToString()));
				continue;
			}

			//////////////////////////////////////////////////////////////////////////////////////////
			//gather node Rows next, so we can automatically generate keys etc, for refs by edges
			Node fromNode = null;
			for (int i = 0; i < inLines.Count; i++) {
				//Note: we have removed blank lines, so 1st chars: alpha is node, \t is edge, \t\t is edge label
				if (char.IsLetter(inLines[i][0])) { //check 1st character of line to skip nodes
					fromNode = nodes.Find(n => n.Name == inLines[i].Trim());
				}
				if (char.IsLetter(inLines[i][0]) || inLines[i].StartsWith("\t\t")) //handle nodes without edges defined
					continue;

				//definitely a row of fromNode
				string ln = inLines[i].Trim().Replace(">>", ">");
				if (ln.Contains(">") == false)
					throw new Exception("Error: Need left and right parts, separated by > character: " + ln);
				string left = ln.Split('>')[0].Trim();
				string right = ln.Split('>')[1].Trim();

				bool fromNodeHasRow = fromNode.Rows.Exists(r => r.Value == left);
				if(!fromNodeHasRow)
					fromNode.AddRow(left);

				Node toNode = nodes.Find(n => n.Name == right.Split(':')[0].Trim());
				if(toNode == null)
					throw(new Exception("Node not found: " + right));
				bool toNodeHasRow = toNode.Rows.Exists(r => r.Value == right.Substring(right.IndexOf(':') + 1));
				if(!toNodeHasRow)
					toNode.AddRow(right.Substring(right.IndexOf(':') + 1));
			}

			//////////////////////////////////////////////////////////////////////////////////////////
			//gather edge details w/ labels
			fromNode = null;
			for (int i = 0; i < inLines.Count; i++) {
				//Note: we have removed blank lines, so 1st chars: alpha is node, \t is edge, \t\t is edge label
				if (char.IsLetter(inLines[i][0])) { //check 1st character of line to skip nodes
					fromNode = nodes.Find(n => n.Name == inLines[i].Trim());
				}
				if (char.IsLetter(inLines[i][0]) || inLines[i].StartsWith("\t\t")) //handle nodes without edges defined
					continue; 

				//definitely an edge of fromNode
				string edge = inLines[i].Trim();
				Edge ed = new Edge(fromNode);
				string fromrowVal = edge.Substring(0, edge.IndexOf(">")).Trim();
				ed.FromNodeRowKey = ed.FromNode.Rows.Find(r => r.Value == fromrowVal).Key;
				ed.Type = PointType.Standard; //default
				if (edge.Contains(">>"))
					ed.Type = PointType.Sync;
				edge = edge.Replace(">>", ">");
				ed.Name = edge.Split('>')[0].Trim();
				string endNodeRaw = edge.Split('>')[1].Trim();
				ed.ToNode = nodes.Find(n => n.Name == endNodeRaw.Split(':')[0].Trim());
				string torowVal = endNodeRaw.Substring(endNodeRaw.IndexOf(':') + 1).Trim();//.Split(':')[1].Trim();
				ed.ToNodeRowKey = ed.ToNode.Rows.Find(r => r.Value == torowVal).Key;
				Edge destNodeEd = new Edge();
				destNodeEd.Name = endNodeRaw.Substring(endNodeRaw.IndexOf(':') + 1);// endNodeRaw.Split(':')[1].Trim();
				//ed.ToNode.Edges.Add(destNodeEd);
				if (inLines.Count > i + 1 && inLines[i + 1].StartsWith("\t\t")) {
					ed.Label = inLines[i + 1].Trim();
				}

				edges.Add(ed);
			}

			//GOOD but untested, we're gathering Nodes fully for export, then Edges, for their export

			//iterate through nodes, build our output for the nodes section
			int nodeNum = 1;
			foreach (Node n in nodes) {
				List<string> eds = new List<string>();
				string edsStr = ""; //<rowAddrKeyA> Text | <rowAddrKeyB> Text

				//iterate through all points, capturing start/end nodes to build 
				string s = string.Format("\"node{0}\" [ #{1}{2}label = {3}{2}shape = \"record\"",
					nodeNum++, //0
					n.Name, //1
					"\n\r", //2
					edsStr); //3
			}
			string result = BuildFinal(nodes, edges);
			return result;
		}

		/// <summary>
		/// http://stackoverflow.com/questions/3499056/making-a-legend-key-in-graphviz
		/// </summary>
		/// <returns></returns>
		private string BuildLegendHtml() {
			string result = string.Format("{{ rank = sink; " +
				"Legend [shape=none, margin=0, label=< " +
				"<TABLE BORDER=\"0\" CELLBORDER=\"1\" CELLSPACING=\"0\" CELLPADDING=\"4\"> " +
					"<TR><TD COLSPAN=\"2\"><B>Legend</B></TD></TR>" +
					"<TR><TD>Black</TD><TD COLOR=\"BLACK\">Sync: COPY/MOVE Data</TD></TR> " +
					"<TR><TD>Red</TD><TD COLOR=\"BLACK\">Lookup: QUERY Data</TD></TR> " +
				"</TABLE>>];}}");
			return result;
		}

		private void BuildSetup() {
			setup.Add("### SETUP DEFAULTS");
			setup.Add("graph [");
			setup.Add("rankdir = \"LR\"");
			setup.Add("ranksep=2");
			setup.Add("splines=true");
			setup.Add("concentrate = true"); // combine edges that come together (req edge minlen >= 2.0)
			setup.Add("];");
			setup.Add("node [");
			setup.Add("fontsize = \"8\"");
			setup.Add("fontname = Helvetica");
			setup.Add("shape = \"ellipse\"");
			setup.Add("];");
			setup.Add("edge [");
			setup.Add("fontsize = \"8\"");
			setup.Add("fontname = Helvetica");
			setup.Add("arrowsize = .8");
			setup.Add("color = \"#ff6666\"");
			setup.Add("arrowhead = normal");
			//setup.Add("minlen = 2"); //separates busy rows' edges nicely
			setup.Add("];");
		}

		private string BuildFinal(List<Node> nodes, List<Edge> edges) {
			string result = start + "\n";
			result += string.Join("\n", setup.ToArray()) + "\n";

			result += BuildLegendHtml();

			//Build nodeLines
			foreach (Node n in nodes) {				
				nodeLines.Add(string.Format("\"{0}\" [ #{1}", n.Id, n.Name));
				//label = "<f0> Filesite| <cm> Clients & Matters"
				List<string> rows = new List<string>();
				rows.Add("<title> " + n.Name);
				foreach (KeyValuePair<string, string> r in n.Rows)
					rows.Add(string.Format("<{0}> {1}", r.Key, r.Value));
				string rowsJoined = string.Join("|", rows);
				nodeLines.Add(string.Format("\tlabel = \"{0}\"", rowsJoined));
				nodeLines.Add(string.Format("\tshape = \"record\""));
				nodeLines.Add("];");
			}
			result += string.Join("\n", nodeLines.ToArray()) + "\n";
			
			//Build edgeLines
			foreach (Edge e in edges) {
				edgeLines.Add(string.Format("\"{0}\":{1} -> \"{2}\":{3}", e.FromNode.Id, e.FromNodeRowKey, e.ToNode.Id, e.ToNodeRowKey));
				if(e.Type == PointType.Sync)
					edgeLines.Add("\t[penwidth=\"1\"] [arrowsize = 1.2] [arrowhead=onormal] [color=black] #[style=\"tapered\" penwidth=\"4\"] #[style=\"bold\"] ");
				edgeLines.Add(string.Format("\t[label = \"{0}\" ]", e.Label));
				
				//edgeLines.Add(string.Format("\t[id = {0}];", e.Id));
			}
			result += string.Join("\n", edgeLines.ToArray()) + "\n";

			result += end;
			return result;
		}
	}

	class Node {
		public string Name { get; set; }
		public string Id { get; set; }
		public List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string,string>>();// { get; set; } //initial will always be <node0> Name
		
		/// <summary>
		/// Checks if val exists already, and adds if not
		/// </summary>
		/// <param name="val"></param>
		public void AddRow(string val) {
			bool asdf = false;
			if (val.Trim().StartsWith("Clients &"))
				asdf = true;

			val = val.Trim();
			if(Rows.Exists(r => r.Value == val) == false)
				Rows.Add(new KeyValuePair<string, string>("r" + Rows.Count.ToString(), val));
		}

		public Node(string name, string id) {
			Name = name;
			Id = id;
		}

		public string ToNodeString() {
			string result = "";

			return result;
		}
	}

	class Edge {
		public string Name { get; set; }
		public PointType Type { get; set; }
		
		public Node FromNode { get; set; }
		public Node ToNode { get; set; }
		public string FromNodeRowKey { get; set; }
		public string ToNodeRowKey { get; set; }
		
		public string Label { get; set; }

		/// <summary>
		/// For destination points (for source points pass in startNode ref)
		/// </summary>
		public Edge() { }

		/// <summary>
		/// This point will reference another (with an arrow in graphvis)
		/// </summary>
		/// <param name="startNode"></param>
		public Edge(Node startNode) {
			FromNode = startNode;
		}
	}

	enum PointType { Sync, Standard };

}
