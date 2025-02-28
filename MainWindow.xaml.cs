using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Routing;
using DrawingNode = Microsoft.Msagl.Drawing.Node;
using LayoutNode = Microsoft.Msagl.Core.Layout.Node;



namespace SifuMoveModdingTool
{
    public partial class MainWindow : Window
    {
        private JObject jsonData;

        private List<MNode> mNodesList = new List<MNode>();
        private List<MAttacksNode> mAttacksNodesList = new List<MAttacksNode>();
        private List<string> nameMap = new List<string>();
        private List<MoveEntry> moveEntries = new List<MoveEntry>(); // Class-level variable to store the list
        private List<ImportsNode> importsNodesList = new List<ImportsNode>();
        private List<ComboNode> comboTrees = new List<ComboNode>();
        

        private Dictionary<string, ComboNode> comboNodeByUniqueId = new Dictionary<string, ComboNode>();
        private static int comboNodeCounter = 0;

        private ComboNode selectedComboNode = null;
        private List<DrawingNode> highlightedDrawingNodes = new List<DrawingNode>();

        private GraphViewer graphViewer;
        private bool isPanning = false;
        private Point panStartPoint;
        private Cursor previousCursor;


        private Microsoft.Msagl.Drawing.Color defaulNodeFillColor = new Microsoft.Msagl.Drawing.Color(230, 230, 230);
        private Microsoft.Msagl.Drawing.Color defaulNodeBrorderColor = new Microsoft.Msagl.Drawing.Color(40, 40, 40);

        private Microsoft.Msagl.Drawing.Color highlightFillColor = new Microsoft.Msagl.Drawing.Color(200, 220, 240);
        private Microsoft.Msagl.Drawing.Color highlightBorderColor = Microsoft.Msagl.Drawing.Color.Black;

        private Microsoft.Msagl.Drawing.Color defaultSubgraphFillColor = new Microsoft.Msagl.Drawing.Color(240, 240, 255);
        private Microsoft.Msagl.Drawing.Color defaultSubgraphBorderColor = new Microsoft.Msagl.Drawing.Color(40, 40, 40);



        public MainWindow()
        {
            InitializeComponent();

            graphViewer = new GraphViewer();

            GraphContainer.Children.Add(graphViewer.GraphCanvas);
            //graphViewer.BindToPanel(GraphContainer);

            graphViewer.MouseDown += GraphViewer_NodeMouseDown;
            graphViewer.GraphCanvas.MouseDown += GraphCanvas_MouseDown;
            graphViewer.GraphCanvas.MouseUp += GraphCanvas_MouseUp;
            graphViewer.GraphCanvas.MouseMove += GraphCanvas_MouseMove;

            SetAttackInfoMode(false);
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string jsonFilePath = openFileDialog.FileName;
                    string jsonText = File.ReadAllText(jsonFilePath);

                    jsonData = JObject.Parse(jsonText);

                    moveEntries = ParseNameMap(jsonData);
                    mNodesList = ParseMNodes(jsonData);
                    importsNodesList = ParseImports(jsonData);
                    mAttacksNodesList = ParseMAttacks(jsonData);

                    UpdateGraph();

                    MessageBox.Show($"Loaded {mNodesList.Count} m_Nodes from the JSON file.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading JSON file: {ex.Message}");
                }
            }
        }
        private void UpdateGraph()
        {
            comboNodeByUniqueId = new Dictionary<string, ComboNode>();
            comboNodeCounter = 0;

            comboTrees = GenerateCombos();

            DisplayImports(importsNodesList);
            DisplayNameMapEntries(moveEntries);
            DisplayCombosGraphMSAGL(comboTrees);
            DisplayTransitionTypeComboBox();
            DisplayTransitionTargets();
            DisplayNewNodeIndex();
        }



        /// <summary>
        /// PARSING
        /// </summary>

        private List<ImportsNode> ParseImports(JObject jsonData)
        {
            var pasedImportsNodes = new List<ImportsNode>();

            JArray importsArray = (JArray)jsonData["Imports"];
            if (importsArray == null)
            {
                MessageBox.Show("Imports array not found in JSON data.");
                return null;
            }

            for (int i = 0; i < importsArray.Count; i++)
            {
                JObject importObj = (JObject)importsArray[i];
                string className = importObj["ClassName"]?.ToString();
                string classPackage = importObj["ClassPackage"]?.ToString();
                string objectName = importObj["ObjectName"]?.ToString();
                int outerIndex = importObj["OuterIndex"] != null ? (int)importObj["OuterIndex"] : 0;
                int innerIndex = -1 - i; // Negative index starting from -1

                ImportsNode importsNode = new ImportsNode
                {
                    ObjectName = objectName,
                    InnerIndex = innerIndex,
                    OuterIndex = outerIndex,
                    IndexInImportsArray = i,
                    ClassName = className,
                    ClassPackage = classPackage
                };

                pasedImportsNodes.Add(importsNode);
            }

            return pasedImportsNodes;
        }

        private List<MNode> ParseMNodes(JObject jsonData)
        {
            List<MNode> parsedmNodesList = new List<MNode>();

            JArray exportsArray = (JArray)jsonData["Exports"];
            if (exportsArray == null)
            {
                MessageBox.Show("Exports array not found in JSON data.");
                return null;
            }

            foreach (JObject exportObj in exportsArray)
            {
                JArray dataArray = (JArray)exportObj["Data"];
                if (dataArray == null) continue;

                foreach (JToken dataToken in dataArray)
                {
                    if (dataToken["Name"]?.ToString() == "m_Nodes")
                    {
                        JArray mNodesArray = (JArray)dataToken["Value"];
                        if (mNodesArray == null) continue;

                        for (int nodeIndex = 0; nodeIndex < mNodesArray.Count; nodeIndex++)
                        {
                            JObject mNodeObj = (JObject)mNodesArray[nodeIndex];
                            if (mNodeObj == null) continue;

                            MNode mNode = new MNode
                            {
                                IndexInMNodesArray = nodeIndex
                            };

                            JArray mNodeValues = (JArray)mNodeObj["Value"];
                            if (mNodeValues == null) continue;

                            foreach (JToken mNodeValueToken in mNodeValues)
                            {
                                string propName = mNodeValueToken["Name"]?.ToString();
                                if (propName == "m_Name")
                                {
                                    mNode.MNodeName = mNodeValueToken["Value"]?.ToString();
                                }
                                else if (propName == "m_AttackInfos")
                                {
                                    ParseAttackInfos(mNodeValueToken, mNode);
                                    // Look in m_AttackInfos for m_ChargedAttacks and m_BuildUpForChargedAttack blocks.
                                    JArray infosArray = mNodeValueToken["Value"] as JArray;
                                    if (infosArray != null)
                                    {
                                        foreach (JToken infoItem in infosArray)
                                        {
                                            string infoName = infoItem["Name"]?.ToString();
                                            if (infoName == "m_ChargedAttacks")
                                            {
                                                var blocks = ParseChargedAttacks(infoItem);
                                                if (blocks != null)
                                                    mNode.ChargedAttacks.AddRange(blocks);
                                            }
                                            else if (infoName == "m_BuildUpForChargedAttack")
                                            {
                                                var blocks = ParseBuildUpForChargedAttack(infoItem);
                                                if (blocks != null && blocks.Any())
                                                    mNode.BuildUpForChargedAttack.AddRange(blocks);
                                            }
                                        }
                                    }
                                }
                                else if (propName == "m_Transitions")
                                {
                                    ParseTransitions(mNodeValueToken, mNode);
                                }
                                else if (propName == "m_ConditionalAttacks")
                                {
                                    mNode.ConditionalAttacks = ParseConditionalAttacks(mNodeValueToken);
                                }
                                else if (propName == "m_RequiredTags")
                                {
                                    // Capture the required tags from the GameplayTagContainer's inner array.
                                    JArray reqTagsHolder = mNodeValueToken["Value"] as JArray;
                                    if (reqTagsHolder != null && reqTagsHolder.Count > 0)
                                    {
                                        JToken tagContainer = reqTagsHolder[0];
                                        JArray tagsValueArray = tagContainer["Value"] as JArray;
                                        if (tagsValueArray != null)
                                        {
                                            foreach (var tag in tagsValueArray)
                                            {
                                                mNode.RequiredTags.Add(tag.ToString());
                                            }
                                        }
                                    }
                                }
                                else if (propName == "m_NodeRedirect")
                                {
                                    mNode.NodeRedirect = mNodeValueToken["Value"] != null ? (int)mNodeValueToken["Value"] : -1;
                                }
                                else if (propName == "m_bSkip")
                                {
                                    mNode.BSkip = mNodeValueToken["Value"] != null ? (bool)mNodeValueToken["Value"] : false;
                                }
                            }
                            parsedmNodesList.Add(mNode);
                        }
                    }
                }
            }
            return parsedmNodesList;
        }

        private void ParseAttackInfos(JToken attackInfosToken, MNode mNode)
        {
            JArray attackInfosArray = attackInfosToken["Value"] as JArray;
            if (attackInfosArray == null) return;

            int attackInfoIndex = 0;

            foreach (JToken attackInfoToken in attackInfosArray)
            {
                if (attackInfoToken["Name"]?.ToString() == "m_Attacks")
                {
                    AttackInfo attackInfo = new AttackInfo
                    {
                        ExtraPackagePath = attackInfoToken["Value"]?.ToString(),
                        AttackInfoIndex = attackInfoIndex
                    };

                    mNode.AttackInfos.Add(attackInfo);
                }
                attackInfoIndex++;
            }
        }

        private void ParseTransitions(JToken transitionsToken, MNode mNode)
        {
            // m_Transitions should be an array of ComboTransitions
            JArray transitionsValueArray = transitionsToken["Value"] as JArray;
            if (transitionsValueArray == null) return;

            foreach (JToken transitionToken in transitionsValueArray)
            {
                string tokenName = transitionToken["Name"]?.ToString();
                if (tokenName == "m_Transitions")
                {
                    JArray transitionsArray = transitionToken["Value"] as JArray;
                    if (transitionsArray == null) continue;

                    foreach (JToken transitionEntry in transitionsArray)
                    {
                        // Each entry is a ComboTransition struct
                        JObject comboTransitionObj = transitionEntry as JObject;
                        if (comboTransitionObj == null) continue;

                        Transition transition = new Transition();
                        // Local list to accumulate target indices
                        List<int> targetIndices = new List<int>();

                        JArray comboTransitionValues = comboTransitionObj["Value"] as JArray;
                        if (comboTransitionValues == null) continue;

                        foreach (JToken comboTransitionValueToken in comboTransitionValues)
                        {
                            string propName = comboTransitionValueToken["Name"]?.ToString();

                            if (propName == "m_eInputTransition")
                            {
                                string value = comboTransitionValueToken["Value"]?.ToString();
                                if (value != null && value.StartsWith("EComboTransition::"))
                                {
                                    value = value.Substring("EComboTransition::".Length);
                                }
                                if (Enum.TryParse(value, out EComboTransition attackType))
                                {
                                    transition.AttackType = attackType;
                                }
                                else
                                {
                                    // Handle unknown or new transition types
                                    transition.AttackType = EComboTransition.Light; // Default or adjust as needed
                                }
                            }
                            else if (propName == "m_TargetNodes")
                            {
                                // m_TargetNodes is a map from a byte key to an int value.
                                JArray targetNodesArray = comboTransitionValueToken["Value"] as JArray;
                                if (targetNodesArray == null) continue;

                                foreach (JToken token in targetNodesArray)
                                {
                                    if (token is JArray keyValueArray && keyValueArray.Count == 2)
                                    {
                                        int keyVal = keyValueArray[0]["Value"] != null ? (int)keyValueArray[0]["Value"] : 255;
                                        int targetNodeIndex = keyValueArray[1]["Value"] != null ? (int)keyValueArray[1]["Value"] : -1;
                                        // Add the target node index for backward compatibility.
                                        targetIndices.Add(targetNodeIndex);
                                        // Instead of a single TargetKey, add each key to the list.
                                        transition.TargetKeys.Add(keyVal);
                                    }
                                }
                            }
                            else if (propName == "m_fProbability")
                            {
                                double probability;
                                double.TryParse(comboTransitionValueToken["Value"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out probability);
                                transition.Probability = probability;
                            }
                            else if (propName == "m_ConditionInstance")
                            {
                                int condVal = 0;
                                int.TryParse(comboTransitionValueToken["Value"]?.ToString(), out condVal);
                                transition.ConditionInstance = condVal;
                            }
                        }

                        if (targetIndices.Any())
                        {
                            // Assign primary target node (first element) for backward compatibility.
                            transition.TargetNodeIndices = targetIndices;
                        }

                        mNode.Transitions.Add(transition);
                    }
                }
                else if (tokenName == "m_ParentNodeName")
                {
                    // Capture the parent node name from the transitions block.
                    string parentName = transitionToken["Value"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(parentName))
                        mNode.ParentNodeName = parentName;
                }
            }
        }

        private List<ChargedAttackBlock> ParseChargedAttacks(JToken infoItem)
        {
            var result = new List<ChargedAttackBlock>();
            // The infoItem is expected to contain a "Value" array with one or more blocks.
            JArray blocksArray = infoItem["Value"] as JArray;
            if (blocksArray == null)
                return result;

            // Each block is expected to be a struct representing "ChargedAttackNameByQuadrants"
            foreach (JToken blockToken in blocksArray)
            {
                // The blockToken’s "Value" should be an array that contains eight items:
                // first four hold the m_Attacks entries and the next four hold the corresponding m_StartRatio entries.
                JArray innerArray = blockToken["Value"] as JArray;
                if (innerArray != null && innerArray.Count >= 8)
                {
                    ChargedAttackBlock block = new ChargedAttackBlock();
                    for (int i = 0; i < 4; i++)
                    {
                        // Get the attack value from the first four items.
                        string attackVal = innerArray[i]["Value"]?.ToString();
                        // And the start ratio from the corresponding (i+4) element.
                        string startRatioVal = innerArray[i + 4]["Value"]?.ToString();
                        block.Entries.Add(new ChargedAttackEntry
                        {
                            Attack = attackVal,
                            StartRatio = startRatioVal
                        });
                    }
                    result.Add(block);
                }
            }
            return result;
        }

        private List<BuildUpAttackBlock> ParseBuildUpForChargedAttack(JToken infoItem)
        {
            var result = new List<BuildUpAttackBlock>();
            JArray blocksArray = infoItem["Value"] as JArray;
            if (blocksArray == null || blocksArray.Count == 0)
                return result;

            foreach (JToken blockToken in blocksArray)
            {
                JArray subArray = blockToken["Value"] as JArray;
                if (subArray == null || subArray.Count < 2)
                    continue;

                // The last element is the generic infos block for this outer block.
                var genericInfoToken = subArray.Last;
                BuildUpGenericInfos blockInfos = new BuildUpGenericInfos();
                JArray infosValues = genericInfoToken["Value"] as JArray;
                if (infosValues != null && infosValues.Count >= 3)
                {
                    // Updated parsing for m_fDurationOfCharging:
                    var durationToken = infosValues[0]["Value"];
                    double duration = 0;
                    if (durationToken != null)
                    {
                        if (durationToken.Type == JTokenType.Float || durationToken.Type == JTokenType.Integer)
                        {
                            duration = durationToken.Value<double>();
                        }
                        else
                        {
                            double.TryParse(durationToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
                        }
                    }
                    blockInfos.DurationOfCharging = duration;
                    double extraTime = 0;
                    if (infosValues[1]["Value"] != null)
                    {
                        double.TryParse(infosValues[1]["Value"].ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out extraTime);
                    }
                    blockInfos.ExtraHoldingTimeAfterChargeIscomplete = extraTime;
                    bool allow;
                    bool.TryParse(infosValues[2]["Value"]?.ToString(), out allow);
                    blockInfos.AllowQuadrantToRecompute = allow;
                }

                // Process each AnimContainer (all elements except the last one)
                int animCount = subArray.Count - 1;
                for (int i = 0; i < animCount; i++)
                {
                    var animToken = subArray[i];
                    AnimContainer anim = new AnimContainer();
                    JArray animValues = animToken["Value"] as JArray;
                    if (animValues != null && animValues.Count >= 5)
                    {
                        int animation;
                        int.TryParse(animValues[0]["Value"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out animation);
                        anim.Animation = animation;
                        bool mirror;
                        bool.TryParse(animValues[1]["Value"]?.ToString(), out mirror);
                        anim.Mirror = mirror;
                        bool loopable;
                        bool.TryParse(animValues[2]["Value"]?.ToString(), out loopable);
                        anim.Loopable = loopable;
                        anim.StartRatio = animValues[3]["Value"]?.ToString();
                        double playRate;
                        double.TryParse(animValues[4]["Value"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out playRate);
                        anim.PlayRate = playRate;
                    }
                    BuildUpAttackBlock block = new BuildUpAttackBlock
                    {
                        AnimContainer = anim,
                        Infos = blockInfos
                    };
                    result.Add(block);
                }
            }
            return result;
        }

        private List<ConditionalAttackEntry> ParseConditionalAttacks(JToken condToken)
        {
            List<ConditionalAttackEntry> conditionalAttacks = new List<ConditionalAttackEntry>();

            JArray pairsArray = condToken["Value"] as JArray;
            if (pairsArray == null)
                return conditionalAttacks;

            foreach (JToken pair in pairsArray)
            {
                JArray keyValuePair = pair as JArray;
                if (keyValuePair == null || keyValuePair.Count < 2)
                    continue;

                // Parse key: expecting a Generic structure with a Value array holding the TagName.
                JToken keyToken = keyValuePair[0];
                string tagName = "";
                JArray keyValueArray = keyToken["Value"] as JArray;
                if (keyValueArray != null && keyValueArray.Count > 0)
                {
                    tagName = keyValueArray[0]["Value"]?.ToString() ?? "";
                }

                // Parse value: expecting a Generic structure whose Value array lists moves.
                List<string> moves = new List<string>();
                JToken valueToken = keyValuePair[1];
                JArray movesArray = valueToken["Value"] as JArray;
                if (movesArray != null)
                {
                    foreach (JToken moveToken in movesArray)
                    {
                        if (moveToken["Name"]?.ToString() == "m_Attacks")
                        {
                            string movePath = moveToken["Value"]?.ToString();
                            if (!string.IsNullOrEmpty(movePath))
                                moves.Add(movePath);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(tagName))
                {
                    ConditionalAttackEntry entry = new ConditionalAttackEntry
                    {
                        TagName = tagName,
                        Moves = moves
                    };
                    conditionalAttacks.Add(entry);
                }
            }

            return conditionalAttacks;
        }

        private List<MoveEntry> ParseNameMap(JObject jsonData)
        {
            JArray nameMapArray = (JArray)jsonData["NameMap"];
            if (nameMapArray == null)
            {
                MessageBox.Show("NameMap not found in JSON data.");
                return null;
            }
            // Store the full list for later writing.
            nameMap = nameMapArray.ToObject<List<string>>();

            // For UI purposes, we build a list of NameMapEntry objects.
            List<MoveEntry> parsedNameMapEntries = new List<MoveEntry>();

            // We iterate over the fullNameMap and use only entries that do not contain '/' as displayable names.
            foreach (string nameEntry in nameMap)
            {
                // Only add “root” names (without any '/' string) to the UI list.
                if (!nameEntry.Contains("/"))
                {
                    string name = nameEntry;

                    // Look up corresponding paths in the full list.
                    string packagePath = nameMap.FirstOrDefault(e => e.EndsWith("/" + name));
                    string extraPackagePath = nameMap.FirstOrDefault(e => e.EndsWith("/" + name + "." + name));

                    // Add to the UI list only if at least one associated path exists.
                    if (packagePath != null || extraPackagePath != null)
                    {
                        MoveEntry entry = new MoveEntry
                        {
                            Name = name,
                            PackagePath = packagePath,
                            ExtraPackagePath = extraPackagePath
                        };
                        parsedNameMapEntries.Add(entry);
                    }
                }
            }
            return parsedNameMapEntries;
        }

        private List<MAttacksNode> ParseMAttacks(JObject jsonData)
        {
            List<MAttacksNode> mAttacksNodesList = new List<MAttacksNode>();

            // Get the Exports array
            JArray exportsArray = (JArray)jsonData["Exports"];
            if (exportsArray == null)
            {
                MessageBox.Show("Exports array not found in JSON data.");
                return mAttacksNodesList;
            }

            // Loop through Exports to find m_Attacks
            foreach (JObject exportObj in exportsArray)
            {
                JArray dataArray = (JArray)exportObj["Data"];
                if (dataArray == null)
                    continue;

                foreach (JToken dataToken in dataArray)
                {
                    if (dataToken["Name"]?.ToString() == "m_Attacks")
                    {
                        // Found the m_Attacks map
                        JArray mAttacksArray = (JArray)dataToken["Value"];
                        if (mAttacksArray == null)
                            continue;

                        // Iterate over the map entries (key-value pairs)
                        for (int entryIndex = 0; entryIndex < mAttacksArray.Count; entryIndex++)
                        {
                            JArray keyValueArray = mAttacksArray[entryIndex] as JArray;
                            if (keyValueArray == null || keyValueArray.Count != 2)
                                continue;

                            // The key is the AttackFullPath
                            JObject keyObj = keyValueArray[0] as JObject;
                            string attackFullPath = keyObj["Value"]?.ToString();

                            // The value is the ValueIndex
                            JObject valueObj = keyValueArray[1] as JObject;
                            int valueIndex = valueObj["Value"] != null ? (int)valueObj["Value"] : 0;

                            MAttacksNode mAttacksNode = new MAttacksNode
                            {
                                AttackFullPath = attackFullPath,
                                ValueIndex = valueIndex,
                                IndexInMAttacksArray = entryIndex
                            };

                            mAttacksNodesList.Add(mAttacksNode);
                        }
                    }
                }
            }

            return mAttacksNodesList;
        }

        
        






        /// <summary>
        /// DISPLAY
        /// </summary>

        private void DisplayImports(List<ImportsNode> importsNodesList)
        {
            List<ImportsDisplay> importsDisplayList = new List<ImportsDisplay>();
            int no = 1;

            var AttackDBsList = importsNodesList.Where(importsnode => importsnode.ClassName == "AttackDB").ToList();

            // Display AttackDB imports
            foreach (var importsNode in AttackDBsList)
            {
                importsDisplayList.Add(new ImportsDisplay
                {
                    No = no++,
                    MoveName = importsNode.ObjectName,
                    InnerIndex = importsNode.InnerIndex,
                    OuterIndex = importsNode.OuterIndex
                });
            }

            // Bind the list to the ImportsDataGrid
            ImportsDataGrid.ItemsSource = importsDisplayList;
        }

        private List<ResultDisplay> GetImportsForSelectedNode(MNode selectedMNode)
        {
            List<ResultDisplay> resultEntries = new List<ResultDisplay>();
            int no = 1;

            // Collect asset names from the AttackInfos
            var attackInfosWithIndex = selectedMNode.AttackInfos
                .Select((ai, index) => new { AttackInfo = ai, Index = index + 1 }) // index + 1 for display
                .ToList();

            foreach (var aiEntry in attackInfosWithIndex)
            {
                var attackInfo = aiEntry.AttackInfo;
                int attackInfoIndex = aiEntry.Index;

                // Extract the asset name from the AttackFullPath
                string assetName = GetAssetNameFromPackagePath(attackInfo.ExtraPackagePath);

                // Find matching ImportsNode(s) in importsData.AttackDBs
                var AttackDBsList = importsNodesList.Where(importsnode => importsnode.ClassName == "AttackDB").ToList();
                var matchingImportsNodes = AttackDBsList
                    .Where(importNode => string.Equals(importNode.ObjectName, assetName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Display the imports associated with this AttackInfo
                foreach (var importsNode in matchingImportsNodes)
                {
                    resultEntries.Add(new ResultDisplay
                    {
                        No = no++,
                        Category = "Import",
                        Detail = $"ObjectName: {importsNode.ObjectName}, InnerIndex: {importsNode.InnerIndex}, OuterIndex: {importsNode.OuterIndex}",
                        AdditionalInfo = $"AttackInfo Index: {attackInfoIndex}"
                    });
                }
            }

            return resultEntries;
        }

        private void DisplayCombosGraphMSAGL(List<ComboNode> allCombos)
        {
            var graph = new Graph();
            var nodeDict = new Dictionary<string, DrawingNode>();

            double yOffset = 0;       // Vertical offset for positioning trees
            double treeSpacing = 300; // Adjust spacing as needed

            foreach (var startingNode in allCombos)
            {
                // Create a subgraph for each tree.
                var subgraph = new Subgraph(startingNode.MNode.MNodeName + "_Subgraph");

                subgraph.LabelText = startingNode.MNode.MNodeName;
                subgraph.Label.FontColor = Microsoft.Msagl.Drawing.Color.Black;
                subgraph.Label.FontSize = 14;
                subgraph.Label.FontName = "Segoe UI";
                subgraph.Attr.FillColor = defaultSubgraphFillColor;
                subgraph.Attr.Color = defaultSubgraphBorderColor;
                subgraph.Attr.LineWidth = 2;

                graph.RootSubgraph.AddSubgraph(subgraph);

                AddDiagramNodeToGraph(startingNode, null, EComboTransition.Light, graph, nodeDict, subgraph);

                var settings = new SugiyamaLayoutSettings();
                settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.SugiyamaSplines;
                //settings.Transformation = PlaneTransformation.Rotation(Math.PI / 2);           //For horizontal tree layout
                settings.NodeSeparation = 10;
                settings.LayerSeparation = 40;

                graph.LayoutAlgorithmSettings = settings;
                yOffset += treeSpacing;
            }

            graphViewer.Graph = graph;
        }


        private void AddDiagramNodeToGraph(
            ComboNode comboNode,
            DrawingNode parentDrawingNode,
            EComboTransition transitionType,
            Graph graph,
            Dictionary<string, DrawingNode> nodeDict,
            Subgraph subgraph)
        {
            if (nodeDict.ContainsKey(comboNode.UniqueId))
            {
                // Node already exists, but we need to connect it with the parent
                if (parentDrawingNode != null)
                {
                    string edgeKey = $"{parentDrawingNode.Id}->{comboNode.UniqueId}:{transitionType}";

                    if (!nodeDict.ContainsKey(edgeKey))
                    {
                        var edge = graph.AddEdge(parentDrawingNode.Id, comboNode.UniqueId);

                        edge.LabelText = transitionType.ToString();
                        edge.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
                        edge.Attr.ArrowheadAtTarget = ArrowStyle.Normal;
                        edge.Label.FontSize = 8;

                        nodeDict[edgeKey] = null;
                    }
                }
                return; // Do not process further to avoid infinite loops
            }

            var drawingNode = new DrawingNode(comboNode.UniqueId)
            {
                LabelText = $"{comboNode.MNode.MNodeName}\n---------\n[{GetAssetNameFromPackagePath(comboNode.MNode.AttackInfos?.FirstOrDefault()?.ExtraPackagePath)}]"
            };

            drawingNode.Attr.Shape = Shape.Box;
            drawingNode.Attr.FillColor = defaulNodeFillColor;
            drawingNode.Attr.Color = defaulNodeBrorderColor;
            drawingNode.Label.FontSize = 8;
            drawingNode.Label.Height = 30;
            drawingNode.Attr.Padding = 50;

            // Add to the graph and dictionary
            graph.AddNode(drawingNode);
            nodeDict[comboNode.UniqueId] = drawingNode;

            // Add the node to the subgraph
            if (subgraph != null)
            {
                subgraph.AddNode(drawingNode);
            }

            if (parentDrawingNode != null)
            {
                string edgeKey = $"{parentDrawingNode.Id}->{drawingNode.Id}:{transitionType}";

                if (!nodeDict.ContainsKey(edgeKey))
                {
                    var edge = graph.AddEdge(parentDrawingNode.Id, drawingNode.Id);

                    edge.LabelText = transitionType.ToString();
                    edge.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
                    edge.Attr.ArrowheadAtTarget = ArrowStyle.Normal;
                    edge.Label.FontSize = 8;

                    nodeDict[edgeKey] = null;
                }
            }

            foreach (var transition in comboNode.Transitions)
            {
                AddDiagramNodeToGraph(transition.TransitionNode, drawingNode, transition.AttackType, graph, nodeDict, subgraph);
            }
        }

        private void DisplayNameMapEntries(List<MoveEntry> nameMapEntries)
        {
            MoveSelectorComboBox.ItemsSource = null;
            var moveNames = nameMapEntries.Select(n => n.Name).OrderBy(n => n).ToList();
            MoveSelectorComboBox.ItemsSource = moveNames;
        }

        private void DisplayTransitionTypeComboBox()
        {
            TransitionTypeComboBox.ItemsSource = Enum.GetValues(typeof(EComboTransition));
        }

        private void DisplayTransitionTargets()
        {
            TransitionTargetComboBox.ItemsSource = mNodesList.Select(m => m.MNodeName).ToList();
        }

        private void DisplayNewNodeIndex()
        {
            NewMNodeNameTextBox.Text = mNodesList.Count.ToString();
        }

        private void DisplayAttackInfos(MNode selectedMNode)
        {
            if (selectedMNode != null)
            {
                // Update the SelectedNodeTextBlock with the selected node's name
                SelectedNodeTextBlock.Text = $"Selected Node: {selectedMNode.MNodeName}";
            }
            else
            {
                SelectedNodeTextBlock.Text = "No node selected.";
            }

            Attack1TextBox.Text = "N/A";
            Attack2TextBox.Text = "N/A";
            Attack3TextBox.Text = "N/A";
            Attack4TextBox.Text = "N/A";

            HashSet<int> attackInfoInnerIndexes = new HashSet<int>();

            // Set the labels based on the AttackInfos in the selected MNode
            for (int i = 0; i < selectedMNode.AttackInfos.Count && i < 4; i++)
            {
                var attackInfo = selectedMNode.AttackInfos[i];
                string assetName = GetAssetNameFromPackagePath(attackInfo.ExtraPackagePath);

                // Find matching ImportsNode entries
                var matchingImports = FindMatchingAttackDBImports(assetName);

                // Extract the InnerIndex values.
                var innerIndexes = matchingImports.Select(importNode => importNode.InnerIndex);
                // Add unique values to the HashSet.
                foreach (var idx in innerIndexes)
                {
                    attackInfoInnerIndexes.Add(idx);
                }

                switch (i)
                {
                    case 0:
                        Attack1TextBox.Text = assetName;
                        break;
                    case 1:
                        Attack2TextBox.Text = assetName;
                        break;
                    case 2:
                        Attack3TextBox.Text = assetName;
                        break;
                    case 3:
                        Attack4TextBox.Text = assetName;
                        break;
                }
            }
            ReplaceImportsComboBox.ItemsSource = attackInfoInnerIndexes;
            // Highlight matching rows in ImportsDataGrid
            HighlightImportsRows(attackInfoInnerIndexes.ToList());
        }

        private void HighlightImportsRows(List<int> attackInfoInnerIndexes)
        {
            foreach (ImportsDisplay importsDisplay in ImportsDataGrid.ItemsSource)
            {
                if (attackInfoInnerIndexes.Contains(importsDisplay.InnerIndex))
                {
                    importsDisplay.IsHighlighted = true;
                }
                else
                {
                    importsDisplay.IsHighlighted = false;
                }
            }
        }

        private void HighlightSimilarNodes(string nodeName)
        {
            // Reset appearance of previously highlighted nodes
            foreach (var drawingNode in highlightedDrawingNodes)
            {
                ResetNodeAppearance(drawingNode);
            }
            highlightedDrawingNodes.Clear();

            // Find all nodes with the same MNodeName
            foreach (var entity in graphViewer.Entities)
            {
                if (entity is IViewerNode viewerNode)
                {
                    var node = viewerNode.DrawingObject as DrawingNode;
                    var name = node.LabelText.Split('\n')[0];

                    if (node != null && name == nodeName && !node.Id.Contains("Subgraph"))
                    {
                        HighlightNode(node);
                        highlightedDrawingNodes.Add(node);
                    }
                }
            }
        }

        private void HighlightNode(DrawingNode drawingNode)
        {
            drawingNode.Attr.FillColor = highlightFillColor;
            drawingNode.Attr.Color = highlightBorderColor;
        }        

        private void ResetNodeAppearance(DrawingNode drawingNode)
        {
            drawingNode.Attr.FillColor = defaulNodeFillColor;
            drawingNode.Attr.Color = defaulNodeBrorderColor; // Reset outline color
        }

        private void FocusOnNodeInGraph(ComboNode comboNode)
        {
            if (comboNode != null)
            {
                // Find the DrawingNode in the graph
                var drawingNode = graphViewer.Graph.FindNode(comboNode.UniqueId);

                if (drawingNode != null)
                {
                    // Update the GraphViewer to reflect changes
                    graphViewer.Invalidate();

                    double zoomFactor = 1.5;

                    graphViewer.NodeToCenterWithScale(drawingNode, zoomFactor);
                    // Bring the node into view
                    graphViewer.GraphCanvas.Focus();
                }
                else
                {
                    MessageBox.Show("Node not found in the graph.");
                }
            }
        }








        private string GetAssetNameFromPackagePath(string PackagePath)
        {
            if (string.IsNullOrEmpty(PackagePath))
                return "";

            var matchingEntry = moveEntries.FirstOrDefault(x => x.ExtraPackagePath == PackagePath || x.PackagePath == PackagePath);

            if (matchingEntry != null)
            {
                return matchingEntry.Name;
            }
            else
            {
                return "None";
            }
        }

        private List<ComboNode> GenerateCombos()
        {
            var allCombos = new List<ComboNode>();
            comboNodeCounter = 0;
            comboNodeByUniqueId.Clear();

            foreach (var mNode in mNodesList)
            {
                var visitedNodes = new HashSet<int>();
                var levelCache = new Dictionary<int, ComboNode>(); // Cache per tree

                // Generate combo tree for this root node
                var comboNode = GenerateCombosForMNode(mNode, visitedNodes, null, levelCache);
                if (comboNode != null)
                {
                    allCombos.Add(comboNode);
                }
            }
            return allCombos;
        }

        private ComboNode GenerateCombosForMNode(
            MNode mNode,
            HashSet<int> visitedNodes,
            ComboNode parent,
            Dictionary<int, ComboNode> nodeCache)
        {
            // Check if the node exists in the cache
            if (nodeCache.TryGetValue(mNode.IndexInMNodesArray, out var existingNode))
            {
                return existingNode;
            }

            // Create the ComboNode
            var comboNode = new ComboNode
            {
                MNode = mNode,
                UniqueId = $"{mNode.MNodeName}_{comboNodeCounter++}"
            };
            comboNode.ParentComboNode = parent;

            // Add to the node cache and global dictionary before any return
            nodeCache[mNode.IndexInMNodesArray] = comboNode;
            comboNodeByUniqueId[comboNode.UniqueId] = comboNode;

            // Mark the node as visited
            visitedNodes.Add(mNode.IndexInMNodesArray);

            // Process transitions
            foreach (var transition in mNode.Transitions)
            {
                var targetMNode = mNodesList.FirstOrDefault(node => node.IndexInMNodesArray == transition.TargetNodeIndices[0]);
                if (targetMNode != null)
                {
                    if (visitedNodes.Contains(targetMNode.IndexInMNodesArray))
                    {
                        // Cycle detected, use existing node from cache
                        var targetComboNode = nodeCache[targetMNode.IndexInMNodesArray];

                        var comboTransition = new ComboTransition
                        {
                            AttackType = transition.AttackType,
                            TargetNodeIndices = transition.TargetNodeIndices,
                            TransitionNode = targetComboNode
                        };

                        comboNode.Transitions.Add(comboTransition);
                    }
                    else
                    {
                        var targetComboNode = GenerateCombosForMNode(targetMNode, visitedNodes, comboNode, nodeCache);

                        if (targetComboNode != null)
                        {
                            var comboTransition = new ComboTransition
                            {
                                AttackType = transition.AttackType,
                                TargetNodeIndices = transition.TargetNodeIndices,
                                TransitionNode = targetComboNode
                            };

                            comboNode.Transitions.Add(comboTransition);
                        }
                    }
                }
            }

            // Unmark the node as visited before returning
            visitedNodes.Remove(mNode.IndexInMNodesArray);

            return comboNode;
        }



        private ComboNode FindComboNodeByUniqueId(string uniqueId, List<ComboNode> comboNodes)
        {
            foreach (var comboNode in comboNodes)
            {
                // Check if the current node matches
                if (comboNode.UniqueId == uniqueId)
                {
                    return comboNode;
                }

                // Recursively search in child nodes
                var foundNode = FindComboNodeByUniqueIdRecursive(uniqueId, comboNode);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            // Node not found
            return null;
        }

        private ComboNode FindComboNodeByUniqueIdRecursive(string uniqueId, ComboNode currentComboNode)
        {
            foreach (var transition in currentComboNode.Transitions)
            {
                var childComboNode = transition.TransitionNode;

                if (childComboNode.UniqueId == uniqueId)
                {
                    return childComboNode;
                }

                // Recursive call
                var foundNode = FindComboNodeByUniqueIdRecursive(uniqueId, childComboNode);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            // Node not found in this branch
            return null;
        }

        private ComboNode FindComboNodeByUniqueId(string uniqueId)
        {
            if (comboNodeByUniqueId.TryGetValue(uniqueId, out var comboNode))
            {
                return comboNode;
            }
            else
            {
                return null;
            }
        }

        private List<ImportsNode> FindMatchingAttackDBImports(string objectName)
        {
            return importsNodesList
                .Where(importNode => importNode.ClassName == "AttackDB" && importNode.ObjectName == objectName)
                .ToList();
        }

        private void ReplaceWithSelectedMove(MNode mNode, string NewObjectName)
        {
            if (selectedComboNode != null)
            {
                var tempAttackInfosPackages = mNode.AttackInfos.Select(a => a.ExtraPackagePath).ToList();

                ReplaceAttackInfos(mNode, NewObjectName);

                if (ReplaceImportsCheckBox.IsChecked == true)
                    ReplaceImports(NewObjectName);

                ReplaceMAttacks(tempAttackInfosPackages, NewObjectName);

                UpdateGraph();

                DisplayAttackInfos(selectedComboNode.MNode);
                HighlightSimilarNodes(selectedComboNode.MNode.MNodeName);
                FocusOnNodeInGraph(selectedComboNode);
            }
        }

        private void ReplaceAttackInfos(MNode mNode, string NewObjectName)
        {
            // For each attack info
            for (int i = 0; i < 4; i++)
            {
                bool isChecked = false;

                switch (i)
                {
                    case 0:
                        isChecked = Attack1CheckBox.IsChecked == true;
                        break;
                    case 1:
                        isChecked = Attack2CheckBox.IsChecked == true;
                        break;
                    case 2:
                        isChecked = Attack3CheckBox.IsChecked == true;
                        break;
                    case 3:
                        isChecked = Attack4CheckBox.IsChecked == true;
                        break;
                }

                if (isChecked)
                {
                    // Find the NameMapEntry for the selected move
                    var newMoveNameMapEntry = moveEntries.FirstOrDefault(n => n.Name == NewObjectName);

                    if (newMoveNameMapEntry != null)
                    {
                        mNode.AttackInfos[i].ExtraPackagePath = newMoveNameMapEntry.ExtraPackagePath;
                    }
                    else
                    {
                        MessageBox.Show($"Move '{NewObjectName}' not found in NameMapEntries.");
                    }
                }
            }
        }
        
        private void ReplaceImports(string NewObjectName)
        {
            // Only do import replacement if the checkbox is checked.
            if (ReplaceImportsCheckBox.IsChecked == true)
            {
                int selectedInnerIndex = 0;
                // Determine which option the user selected.
                if (SelectImportRadioButton.IsChecked == true)
                {
                    if (ReplaceImportsComboBox.SelectedItem == null)
                    {
                        MessageBox.Show("No matching import selected. Please select one, or use manual entry.",
                                        "Replace Imports", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    selectedInnerIndex = GetSelectedInnerIndex(ReplaceImportsComboBox);
                    if (selectedInnerIndex == 0)
                    {
                        MessageBox.Show("Invalid import selection.", "Replace Imports", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else if (ManualImportRadioButton.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(ManualImportTextBox.Text) ||
                        !int.TryParse(ManualImportTextBox.Text.Trim(), out selectedInnerIndex))
                    {
                        MessageBox.Show("Please enter a valid negative index for the import entry.",
                                        "Replace Imports", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Find the import node with the matching InnerIndex.
                var importNodeWithOldObjectName = importsNodesList.FirstOrDefault(n => n.InnerIndex == selectedInnerIndex);
                var newMoveNameMapEntry = moveEntries.FirstOrDefault(n => n.Name == NewObjectName);

                if (importNodeWithOldObjectName != null)
                {
                    // Replace the ObjectName with the new move.
                    importNodeWithOldObjectName.ObjectName = NewObjectName;

                    if (newMoveNameMapEntry != null)
                    {
                        // Look for an existing import node whose ObjectName matches the new move's package path.
                        var outerNameMapEntry = importsNodesList.FirstOrDefault(n => n.ObjectName == newMoveNameMapEntry.PackagePath);

                        if (outerNameMapEntry != null)
                        {
                            importNodeWithOldObjectName.OuterIndex = outerNameMapEntry.InnerIndex;
                        }
                        else
                        {
                            // If not found, we create a new ImportsNode.
                            int newInnerIndex = importsNodesList.Last().InnerIndex - 1;
                            var newImportsNode = new ImportsNode
                            {
                                ObjectName = newMoveNameMapEntry.PackagePath,
                                InnerIndex = newInnerIndex,
                                OuterIndex = 0,
                                IndexInImportsArray = importsNodesList.Count,
                                ClassName = "Package",
                                ClassPackage = "/Script/CoreUObject"
                            };

                            importsNodesList.Add(newImportsNode);
                            importNodeWithOldObjectName.OuterIndex = newImportsNode.InnerIndex;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Failed to replace Imports for the selected option.", "Replace Imports", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // else: if the checkbox is unchecked, do not modify imports.
        }

        private void ReplaceMAttacks(List<string> AttackInfosPackages, string NewObjectName)
        {
            // For each attack info
            for (int i = 0; i < 4; i++)
            {
                bool isChecked = false;
                int selectedInnerIndex = 0;

                int countOfIndices = 0;

                selectedInnerIndex = GetSelectedInnerIndex(ReplaceImportsComboBox);

                switch (i)
                {
                    case 0:
                        isChecked = Attack1CheckBox.IsChecked == true;
                        //countOfIndeces = Attack1ComboBox.Items.Count;                        
                        break;
                    case 1:
                        isChecked = Attack2CheckBox.IsChecked == true;
                        break;
                    case 2:
                        isChecked = Attack3CheckBox.IsChecked == true;
                        break;
                    case 3:
                        isChecked = Attack4CheckBox.IsChecked == true;
                        break;
                }

                if (isChecked)
                {
                    var oldMatchingMAttacksNodes = mAttacksNodesList.Where(ma => ma.AttackFullPath == AttackInfosPackages[i]).ToList();
                    var newMatchingMAttacksNodes = mAttacksNodesList.Where(ma => ma.AttackFullPath == moveEntries.FirstOrDefault(n => n.Name == NewObjectName).ExtraPackagePath).ToList();

                    var foundImportsNode = importsNodesList.FirstOrDefault(importNode => importNode.InnerIndex == selectedInnerIndex);
                    countOfIndices = FindMatchingAttackDBImports(NewObjectName).Count;


                    if (newMatchingMAttacksNodes != null && oldMatchingMAttacksNodes != null && foundImportsNode != null)
                    {
                        if (newMatchingMAttacksNodes.Count > 0)
                        {
                            // We found mAttacks that already have our new package path
                            continue;
                        }
                        else
                        {
                            // We didn't find any mAttacks that have our new package path
                            // Check how many mAttacks have old package path and how many Imports have that package path
                            if (oldMatchingMAttacksNodes.Count > 1 && countOfIndices < 2)
                            {
                                // We found multiple existing mAttacks
                                // Modify one of the them
                                var foundNode = oldMatchingMAttacksNodes.FirstOrDefault();
                                foundNode.ValueIndex = foundImportsNode.InnerIndex;
                                foundNode.IndexInMAttacksArray = oldMatchingMAttacksNodes.FirstOrDefault().IndexInMAttacksArray;
                                foundNode.AttackFullPath = moveEntries.FirstOrDefault(n => n.Name == foundImportsNode.ObjectName).ExtraPackagePath;
                            }
                            else
                            {
                                // Create new mAttack
                                MAttacksNode mAttacksNode = new MAttacksNode
                                {
                                    ValueIndex = foundImportsNode.InnerIndex,
                                    IndexInMAttacksArray = mAttacksNodesList.Count,
                                    AttackFullPath = moveEntries.FirstOrDefault(n => n.Name == foundImportsNode.ObjectName).ExtraPackagePath
                                };
                                mAttacksNodesList.Add(mAttacksNode);
                            }
                        }
                    }
                    else { MessageBox.Show($"Failed to replace mAttacks for AttackInfo {i + 1} "); }
                }
            }
        }

        private int GetSelectedInnerIndex(ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                if (int.TryParse(comboBox.SelectedItem.ToString(), out int selectedIndex))
                {
                    return selectedIndex;
                }
                else
                {
                    MessageBox.Show("Please enter a valid index for the move.");
                    return 0;
                }
            }

            return 0;
        }

        private List<ComboTransition> GetPathToComboNode(ComboNode comboNode)
        {
            var path = new List<ComboTransition>();
            var current = comboNode;

            while (current.ParentComboNode != null)
            {
                var parent = current.ParentComboNode;
                var transition = parent.Transitions.FirstOrDefault(t => t.TransitionNode == current);

                if (transition != null)
                {
                    path.Insert(0, new ComboTransition
                    {
                        AttackType = transition.AttackType,
                        TransitionNode = current
                    });
                }

                current = parent;
            }

            // Include the root node's MNodeName if needed
            path.Insert(0, new ComboTransition
            {
                AttackType = EComboTransition.Light, // Or null if root
                TransitionNode = current
            });

            return path;
        }

        private ComboNode FindComboNodeByPath(List<ComboTransition> path, List<ComboNode> comboTrees)
        {
            foreach (var rootComboNode in comboTrees)
            {
                if (rootComboNode.MNode.MNodeName == path[0].TransitionNode.MNode.MNodeName)
                {
                    var foundNode = TraversePath(rootComboNode, path, 1);
                    if (foundNode != null)
                    {
                        return foundNode;
                    }
                }
            }
            return null;
        }

        private ComboNode TraversePath(ComboNode currentNode, List<ComboTransition> path, int pathIndex)
        {
            if (pathIndex >= path.Count)
            {
                return currentNode; // Reached the target node
            }

            var nextPathComponent = path[pathIndex];
            foreach (var transition in currentNode.Transitions)
            {
                if (transition.AttackType == nextPathComponent.AttackType &&
                    transition.TransitionNode.MNode.MNodeName == nextPathComponent.TransitionNode.MNode.MNodeName)
                {
                    // Continue traversal
                    var result = TraversePath(transition.TransitionNode, path, pathIndex + 1);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }




        ///////////////////////
        // WRITE TO JSON LOGIC
        ///////////////////////
        private void WriteToJSON(string outputFilePath)
        {
            // Update sections of the original jsonData JObject.
            WriteNameMap(jsonData);
            WriteImports(jsonData);
            WriteMNodes(jsonData);
            WriteMAttacks(jsonData);

            // Write the entire JObject to file.
            System.IO.File.WriteAllText(outputFilePath, jsonData.ToString());
        }

        private void WriteNameMap(JObject json)
        {
            // If originalNameMap has been set, write it back.
            if (nameMap != null)
            {
                JArray newNameMap = JArray.FromObject(nameMap);
                json["NameMap"] = newNameMap;
            }
        }

        private void WriteImports(JObject json)
        {
            // Build a new JArray to represent the Imports data.
            JArray newImportsArray = new JArray();
            foreach (var imp in importsNodesList)
            {
                // Use the Import template from our TemplateProvider, and update the fields.
                JObject importEntry = TemplateProvider.GetNewImportTemplate();
                importEntry["ObjectName"] = imp.ObjectName;
                importEntry["OuterIndex"] = imp.OuterIndex;
                importEntry["ClassPackage"] = imp.ClassPackage;
                importEntry["ClassName"] = imp.ClassName;
                // PackageName remains null and bImportOptional stays false as in the template.
                newImportsArray.Add(importEntry);
            }
            json["Imports"] = newImportsArray;
        }

        private void WriteMAttacks(JObject json)
        {
            JArray newMAttacksArray = new JArray();
            foreach (var mattack in mAttacksNodesList)
            {
                JArray attackTemplate = TemplateProvider.GetNewMAttacksTemplate();
                // Update the first element (NamePropertyData); this holds the attack's full path.
                JObject nameProp = (JObject)attackTemplate[0];
                nameProp["Value"] = mattack.AttackFullPath;

                // Update the second element (ObjectPropertyData); this holds the ValueIndex.
                JObject objProp = (JObject)attackTemplate[1];
                objProp["Value"] = mattack.ValueIndex;

                // Add the updated template to our MAttacks array.
                newMAttacksArray.Add(attackTemplate);
            }

            // Update the location in the JSON where MAttacks are stored.
            // Typically, this would be under an Export->Data object whose "Name" is "m_Attacks".
            JArray exportsArray = (JArray)json["Exports"];
            if (exportsArray != null)
            {
                foreach (JObject exportObj in exportsArray)
                {
                    JArray dataArray = (JArray)exportObj["Data"];
                    if (dataArray != null)
                    {
                        foreach (JObject dataObj in dataArray)
                        {
                            if ((string)dataObj["Name"] == "m_Attacks")
                            {
                                dataObj["Value"] = newMAttacksArray;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void WriteMNodes(JObject json)
        {
            // Build a new array of nodes
            JArray newMNodesArray = new JArray();
            foreach (var node in mNodesList)
            {
                JObject mNodeJson = BuildMNodeJson(node);
                newMNodesArray.Add(mNodeJson);
            }

            // Update the Exports section
            JArray exportsArray = (JArray)json["Exports"];
            if (exportsArray != null)
            {
                foreach (JObject exportObj in exportsArray)
                {
                    JArray dataArray = (JArray)exportObj["Data"];
                    if (dataArray != null)
                    {
                        foreach (JObject dataObj in dataArray)
                        {
                            if ((string)dataObj["Name"] == "m_Nodes")
                            {
                                dataObj["Value"] = newMNodesArray;
                                break;
                            }
                        }
                    }
                }
            }
        }



        private JObject BuildMNodeJson(MNode node)
        {
            // Clone our m_Nodes template
            JObject mNodeTemplate = TemplateProvider.GetNewMNodeTemplate();
            // Create an array to hold the property blocks
            JArray propsArray = new JArray();

            // Fill in each sub‑property using helper functions.
            propsArray.Add(BuildRequiredTags(node));
            propsArray.Add(BuildAttackInfos(node));
            propsArray.Add(BuildConditionalAttacks(node));
            propsArray.Add(BuildAIAttackAction(node));
            propsArray.Add(BuildTransitions(node));
            propsArray.Add(BuildNodeRedirect(node));
            propsArray.Add(BuildAvailabilityLayer(node));
            propsArray.Add(BuildRedirectIgnoredTransitions(node));
            propsArray.Add(BuildEvent(node));
            propsArray.Add(BuildName(node));
            propsArray.Add(BuildSkip(node));

            mNodeTemplate["Value"] = propsArray;
            return mNodeTemplate;
        }

        private JObject BuildRequiredTags(MNode node)
        {
            // This template preserves required tags.
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
              ""StructType"": ""GameplayTagContainer"",
              ""SerializeNone"": true,
              ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
              ""Name"": ""m_RequiredTags"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": [
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Structs.GameplayTagContainerPropertyData, UAssetAPI"",
                  ""Name"": ""m_RequiredTags"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": []
                }
              ]
            }";
            JObject tags = JObject.Parse(template);
            JArray tagArray = new JArray();
            if (node.RequiredTags != null)
            {
                foreach (var tag in node.RequiredTags)
                    tagArray.Add(tag);
            }
            tags["Value"][0]["Value"] = tagArray;
            return tags;
        }

        private JObject BuildAttackInfos(MNode node)
        {
            JArray attackInfosArray = new JArray();
            for (int i = 0; i < node.AttackInfos.Count; i++)
            {
                var attack = node.AttackInfos[i];
                JObject attackObj = new JObject();
                attackObj["$type"] = "UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI";
                attackObj["Name"] = "m_Attacks";
                attackObj["DuplicationIndex"] = i;
                attackObj["IsZero"] = false;
                attackObj["Value"] = string.IsNullOrWhiteSpace(attack.ExtraPackagePath) ? "None" : attack.ExtraPackagePath;
                attackInfosArray.Add(attackObj);
            }

            // Append ChargedAttacks block using a helper.
            attackInfosArray.Add(BuildChargedAttacksBlock(node));

            // Append BuildUpForChargedAttack block using a helper.
            attackInfosArray.Add(BuildBuildUpForChargedAttackBlock(node));

            JObject mAttackInfos = new JObject();
            mAttackInfos["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
            mAttackInfos["StructType"] = "ComboNodeAttackInfos";
            mAttackInfos["SerializeNone"] = true;
            mAttackInfos["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
            mAttackInfos["Name"] = "m_AttackInfos";
            mAttackInfos["DuplicationIndex"] = 0;
            mAttackInfos["IsZero"] = false;
            mAttackInfos["Value"] = attackInfosArray;
            return mAttackInfos;
        }

        private JObject BuildChargedAttacksBlock(MNode node)
        {
            // Build the m_ChargedAttacks block from node.ChargedAttacks.
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI"",
              ""ArrayType"": ""StructProperty"",
              ""DummyStruct"": {
                  ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                  ""StructType"": ""ChargedAttackNameByQuadrants"",
                  ""SerializeNone"": true,
                  ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                  ""Name"": ""m_ChargedAttacks"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": []
              },
              ""Name"": ""m_ChargedAttacks"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": []
            }";
            JObject blockTemplate = JObject.Parse(template);
            JArray blocksArray = new JArray();

            if (node.ChargedAttacks != null)
            {
                int blockIndex = 0;
                foreach (var block in node.ChargedAttacks)
                {
                    JObject blockObj = new JObject();
                    blockObj["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
                    blockObj["StructType"] = "ChargedAttackNameByQuadrants";
                    blockObj["SerializeNone"] = true;
                    blockObj["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
                    blockObj["Name"] = "m_ChargedAttacks";
                    blockObj["DuplicationIndex"] = 0;
                    blockObj["IsZero"] = false;

                    JArray innerArray = new JArray();
                    // Add attack entries.
                    for (int i = 0; i < block.Entries.Count; i++)
                    {
                        JObject attackObj = new JObject();
                        attackObj["$type"] = "UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI";
                        attackObj["Name"] = "m_Attacks";
                        attackObj["DuplicationIndex"] = i;
                        attackObj["IsZero"] = false;
                        attackObj["Value"] = block.Entries[i].Attack;
                        innerArray.Add(attackObj);
                    }
                    // Add corresponding start ratio entries.
                    for (int i = 0; i < block.Entries.Count; i++)
                    {
                        JObject ratioObj = new JObject();
                        ratioObj["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                        ratioObj["Value"] = block.Entries[i].StartRatio;
                        ratioObj["Name"] = "m_StartRatio";
                        ratioObj["DuplicationIndex"] = i;
                        ratioObj["IsZero"] = false;
                        innerArray.Add(ratioObj);
                    }

                    blockObj["Value"] = innerArray;
                    blocksArray.Add(blockObj);
                    blockIndex++;
                }
            }
            blockTemplate["Value"] = blocksArray;
            // If there is any block, remove the DummyStruct property.
            if (blocksArray.Count > 0)
            {
                blockTemplate.Remove("DummyStruct");
            }
            return blockTemplate;
        }

        private JObject BuildBuildUpForChargedAttackBlock(MNode node)
        {
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI"",
              ""ArrayType"": ""StructProperty"",
              ""Name"": ""m_BuildUpForChargedAttack"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": []
            }";
            JObject templateObj = JObject.Parse(template);
            JArray buildupArray = new JArray();

            if (node.BuildUpForChargedAttack != null && node.BuildUpForChargedAttack.Count > 0)
            {
                // Group the BuildUpAttackBlock items by their generic infos.
                var groups = node.BuildUpForChargedAttack.GroupBy(b => new {
                    Duration = b.Infos.DurationOfCharging,
                    Extra = b.Infos.ExtraHoldingTimeAfterChargeIscomplete,
                    Allow = b.Infos.AllowQuadrantToRecompute
                });

                int groupIndex = 0;
                foreach (var group in groups)
                {
                    JObject blockObj = new JObject();
                    blockObj["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
                    blockObj["StructType"] = "ChargedBuildUpStructByQuadrant";
                    blockObj["SerializeNone"] = true;
                    blockObj["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
                    blockObj["Name"] = "m_BuildUpForChargedAttack";
                    blockObj["DuplicationIndex"] = 0;
                    blockObj["IsZero"] = false;

                    JArray blockValue = new JArray();
                    int animIndex = 0;
                    foreach (var item in group)
                    {
                        JObject animContainer = new JObject();
                        animContainer["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
                        animContainer["StructType"] = "AnimContainer";
                        animContainer["SerializeNone"] = true;
                        animContainer["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
                        animContainer["Name"] = "m_BuildUpByQuadrant";
                        animContainer["DuplicationIndex"] = animIndex;
                        animContainer["IsZero"] = false;
                        JArray animValues = new JArray();

                        JObject m_animation = new JObject();
                        m_animation["$type"] = "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI";
                        m_animation["Name"] = "m_animation";
                        m_animation["DuplicationIndex"] = 0;
                        m_animation["IsZero"] = false;
                        m_animation["Value"] = item.AnimContainer.Animation;
                        animValues.Add(m_animation);

                        JObject m_bMirror = new JObject();
                        m_bMirror["$type"] = "UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI";
                        m_bMirror["Name"] = "m_bMirror";
                        m_bMirror["DuplicationIndex"] = 0;
                        m_bMirror["IsZero"] = false;
                        m_bMirror["Value"] = item.AnimContainer.Mirror;
                        animValues.Add(m_bMirror);

                        JObject m_bLoopable = new JObject();
                        m_bLoopable["$type"] = "UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI";
                        m_bLoopable["Name"] = "m_bLoopable";
                        m_bLoopable["DuplicationIndex"] = 0;
                        m_bLoopable["IsZero"] = false;
                        m_bLoopable["Value"] = item.AnimContainer.Loopable;
                        animValues.Add(m_bLoopable);

                        JObject m_fStartRatio = new JObject();
                        m_fStartRatio["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                        m_fStartRatio["Value"] = item.AnimContainer.StartRatio;
                        m_fStartRatio["Name"] = "m_fStartRatio";
                        m_fStartRatio["DuplicationIndex"] = 0;
                        m_fStartRatio["IsZero"] = false;
                        animValues.Add(m_fStartRatio);

                        JObject m_fPlayRate = new JObject();
                        m_fPlayRate["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                        m_fPlayRate["Value"] = item.AnimContainer.PlayRate;
                        m_fPlayRate["Name"] = "m_fPlayRate";
                        m_fPlayRate["DuplicationIndex"] = 0;
                        m_fPlayRate["IsZero"] = false;
                        animValues.Add(m_fPlayRate);

                        animContainer["Value"] = animValues;
                        blockValue.Add(animContainer);
                        animIndex++;
                    }

                    // Append one generic infos block (always as the last element).
                    var genericInfos = group.First().Infos;
                    JObject genericInfosObj = new JObject();
                    genericInfosObj["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
                    genericInfosObj["StructType"] = "ChargedBuildUpStructGenericInfos";
                    genericInfosObj["SerializeNone"] = true;
                    genericInfosObj["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
                    genericInfosObj["Name"] = "m_Infos";
                    genericInfosObj["DuplicationIndex"] = 0;
                    genericInfosObj["IsZero"] = false;
                    JArray infosValues = new JArray();

                    JObject m_fDuration = new JObject();
                    m_fDuration["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                    m_fDuration["Value"] = genericInfos.DurationOfCharging;
                    m_fDuration["Name"] = "m_fDurationOfCharging";
                    m_fDuration["DuplicationIndex"] = 0;
                    m_fDuration["IsZero"] = false;
                    infosValues.Add(m_fDuration);

                    JObject m_fExtra = new JObject();
                    m_fExtra["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                    if (genericInfos.ExtraHoldingTimeAfterChargeIscomplete == 0)
                        m_fExtra["Value"] = "+0";
                    else
                        m_fExtra["Value"] = genericInfos.ExtraHoldingTimeAfterChargeIscomplete;
                    m_fExtra["Name"] = "m_fExtraHoldingTimeAfterChargeIscomplete";
                    m_fExtra["DuplicationIndex"] = 0;
                    m_fExtra["IsZero"] = false;
                    infosValues.Add(m_fExtra);

                    JObject m_bAllow = new JObject();
                    m_bAllow["$type"] = "UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI";
                    m_bAllow["Name"] = "m_bAllowQuadrantToRecompute";
                    m_bAllow["DuplicationIndex"] = 0;
                    m_bAllow["IsZero"] = false;
                    m_bAllow["Value"] = genericInfos.AllowQuadrantToRecompute;
                    infosValues.Add(m_bAllow);

                    genericInfosObj["Value"] = infosValues;
                    blockValue.Add(genericInfosObj);

                    blockObj["Value"] = blockValue;
                    buildupArray.Add(blockObj);
                    groupIndex++;
                }
                templateObj.Remove("DummyStruct");
                templateObj["Value"] = buildupArray;
                return templateObj;
            }
            else
            {
                // If no BuildUpForChargedAttack data exists, we insert the DummyStruct property.
                JObject dummy = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                    ""StructType"": ""ChargedBuildUpStructByQuadrant"",
                    ""SerializeNone"": true,
                    ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                    ""Name"": ""m_BuildUpForChargedAttack"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": []
                }");

                // Rebuild the template with the DummyStruct property inserted first.
                JObject newTemplateObj = new JObject();
                newTemplateObj.Add("$type", templateObj["$type"]);
                newTemplateObj.Add("ArrayType", templateObj["ArrayType"]);
                newTemplateObj.Add("DummyStruct", dummy);
                newTemplateObj.Add("Name", templateObj["Name"]);
                newTemplateObj.Add("DuplicationIndex", templateObj["DuplicationIndex"]);
                newTemplateObj.Add("IsZero", templateObj["IsZero"]);
                newTemplateObj.Add("Value", new JArray());
                return newTemplateObj;
            }
        }

        private JObject BuildConditionalAttacks(MNode node)
        {
            // If our node has no conditional attacks, return the default empty map.
            if (node.ConditionalAttacks == null || node.ConditionalAttacks.Count == 0)
            {
                string emptyMap = @"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Objects.MapPropertyData, UAssetAPI"",
                    ""Value"": [],
                    ""KeyType"": ""StructProperty"",
                    ""ValueType"": ""StructProperty"",
                    ""KeysToRemove"": [],
                    ""Name"": ""m_ConditionalAttacks"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false
                }";
                return JObject.Parse(emptyMap);
            }

            JArray mapPairs = new JArray();

            // For each conditional attack stored in our node...
            foreach (var cond in node.ConditionalAttacks)
            {
                // Build the key block as a Generic struct containing the TagName.
                JObject keyBlock = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                    ""StructType"": ""Generic"",
                    ""SerializeNone"": true,
                    ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                    ""Name"": ""m_ConditionalAttacks"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": [
                        {
                            ""$type"": ""UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI"",
                            ""Name"": ""TagName"",
                            ""DuplicationIndex"": 0,
                            ""IsZero"": false,
                            ""Value"": """"
                        }
                    ]
                }");
                // Set the tag value.
                keyBlock["Value"][0]["Value"] = cond.TagName;

                // Build the value block. This is also a Generic struct.
                // Its Value array will contain:
                //    • One m_Attacks entry per move in the conditional attack.
                //    • One dummy m_ChargedAttacks block.
                //    • One dummy m_BuildUpForChargedAttack block.
                JObject valueBlock = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                    ""StructType"": ""Generic"",
                    ""SerializeNone"": true,
                    ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                    ""Name"": ""m_ConditionalAttacks"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": []
                }");

                JArray valueItems = new JArray();
                // Add the moves.
                if (cond.Moves != null)
                {
                    int moveIndex = 0;
                    foreach (var move in cond.Moves)
                    {
                        JObject moveObj = new JObject();
                        moveObj["$type"] = "UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI";
                        moveObj["Name"] = "m_Attacks";
                        moveObj["DuplicationIndex"] = moveIndex;
                        moveObj["IsZero"] = false;
                        moveObj["Value"] = move;
                        valueItems.Add(moveObj);
                        moveIndex++;
                    }
                }

                // Add a dummy m_ChargedAttacks block.
                JObject chargedDummy = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI"",
                    ""ArrayType"": ""StructProperty"",
                    ""DummyStruct"": {
                        ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                        ""StructType"": ""ChargedAttackNameByQuadrants"",
                        ""SerializeNone"": true,
                        ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                        ""Name"": ""m_ChargedAttacks"",
                        ""DuplicationIndex"": 0,
                        ""IsZero"": false,
                        ""Value"": []
                    },
                    ""Name"": ""m_ChargedAttacks"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": []
                }");
                valueItems.Add(chargedDummy);

                // Add a dummy m_BuildUpForChargedAttack block.
                JObject buildupDummy = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI"",
                    ""ArrayType"": ""StructProperty"",
                    ""DummyStruct"": {
                        ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                        ""StructType"": ""ChargedBuildUpStructByQuadrant"",
                        ""SerializeNone"": true,
                        ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                        ""Name"": ""m_BuildUpForChargedAttack"",
                        ""DuplicationIndex"": 0,
                        ""IsZero"": false,
                        ""Value"": []
                    },
                    ""Name"": ""m_BuildUpForChargedAttack"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": []
                }");
                valueItems.Add(buildupDummy);

                // Set the value block's Value.
                valueBlock["Value"] = valueItems;

                // Each map entry is stored as an array of two elements: [key, value].
                JArray mapPair = new JArray { keyBlock, valueBlock };
                mapPairs.Add(mapPair);
            }

            // Finally build the outer MapProperty.
            JObject mapProperty = JObject.Parse(@"
            {
                ""$type"": ""UAssetAPI.PropertyTypes.Objects.MapPropertyData, UAssetAPI"",
                ""Value"": [],
                ""KeyType"": ""StructProperty"",
                ""ValueType"": ""StructProperty"",
                ""KeysToRemove"": [],
                ""Name"": ""m_ConditionalAttacks"",
                ""DuplicationIndex"": 0,
                ""IsZero"": false
            }");
            // Optionally remove KeyType and ValueType if our mapEntries are not empty.
            if (mapPairs.Count > 0)
            {
                mapProperty.Remove("KeyType");
                mapProperty.Remove("ValueType");
            }
            mapProperty["Value"] = mapPairs;
            return mapProperty;
        }

        private JObject BuildAIAttackAction(MNode node)
        {
            // Return a fixed template (or build from node data if needed)
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
              ""StructType"": ""AIActionAttackClassInstance"",
              ""SerializeNone"": true,
              ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
              ""Name"": ""m_AIAttackAction"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": [
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI"",
                  ""Name"": ""m_DefaultClassObject"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": 0
                },
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI"",
                  ""Name"": ""m_Instance"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": 0
                }
              ]
            }";
            return JObject.Parse(template);
        }

        private JObject BuildTransitions(MNode node)
        {
            // Create an array to hold all individual transition entries
            JArray transitionEntries = new JArray();
            int dupIndex = 0;
            if (node.Transitions != null && node.Transitions.Count > 0)
            {
                foreach (var transition in node.Transitions)
                {
                    // Build each transition block.
                    JObject transitionObj = new JObject();
                    transitionObj["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
                    transitionObj["StructType"] = "ComboTransition";
                    transitionObj["SerializeNone"] = true;
                    transitionObj["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
                    transitionObj["Name"] = "m_Transitions";
                    transitionObj["DuplicationIndex"] = 0;
                    transitionObj["IsZero"] = false;

                    // Build the value array for each transition.
                    JArray valueArray = new JArray();

                    // 1. Transition enum element.
                    JObject enumObj = new JObject();
                    enumObj["$type"] = "UAssetAPI.PropertyTypes.Objects.EnumPropertyData, UAssetAPI";
                    enumObj["EnumType"] = "EComboTransition";
                    enumObj["InnerType"] = null;
                    enumObj["Name"] = "m_eInputTransition";
                    enumObj["DuplicationIndex"] = 0;
                    enumObj["IsZero"] = false;
                    enumObj["Value"] = $"EComboTransition::{transition.AttackType.ToString()}";

                    // 2. Condition instance element.
                    JObject condObj = new JObject();
                    condObj["$type"] = "UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI";
                    condObj["Name"] = "m_ConditionInstance";
                    condObj["DuplicationIndex"] = 0;
                    condObj["IsZero"] = false;
                    condObj["Value"] = transition.ConditionInstance;

                    // 3. Target nodes map element.
                    JObject targetNodesObj = new JObject();
                    targetNodesObj["$type"] = "UAssetAPI.PropertyTypes.Objects.MapPropertyData, UAssetAPI";
                    
                    JArray mapValueArray = new JArray();

                    // Check our model’s target node indices.
                    if (transition.TargetNodeIndices != null && transition.TargetNodeIndices.Count > 0)
                    {
                        // For each target index, add a key/value pair:
                        for (int i = 0; i < transition.TargetNodeIndices.Count; i++)
                        {
                            int targetValue = transition.TargetNodeIndices[i];
                            JArray kvPair = new JArray();

                            // The key object for each pair.
                            JObject keyObj = new JObject();
                            keyObj["$type"] = "UAssetAPI.PropertyTypes.Objects.BytePropertyData, UAssetAPI";
                            keyObj["ByteType"] = "Byte";
                            keyObj["EnumType"] = null;
                            keyObj["Value"] = (transition.TargetKeys != null && i < transition.TargetKeys.Count) ? transition.TargetKeys[i] : 0;

                            // The value object.
                            JObject valObj = new JObject();
                            valObj["$type"] = "UAssetAPI.PropertyTypes.Objects.IntPropertyData, UAssetAPI";
                            valObj["Name"] = "m_TargetNodes";
                            valObj["DuplicationIndex"] = 0;
                            valObj["IsZero"] = false;
                            valObj["Value"] = targetValue;

                            keyObj["Name"] = "m_TargetNodes";
                            keyObj["DuplicationIndex"] = 0;
                            keyObj["IsZero"] = false;

                            kvPair.Add(keyObj);
                            kvPair.Add(valObj);
                            mapValueArray.Add(kvPair);
                        }
                    }
                    
                    // Set the target nodes map’s Value to our array of pairs.
                    targetNodesObj["Value"] = mapValueArray;
                    // Also include an empty array for KeysToRemove.
                    targetNodesObj["KeysToRemove"] = new JArray();
                    targetNodesObj["Name"] = "m_TargetNodes";
                    targetNodesObj["DuplicationIndex"] = 0;
                    targetNodesObj["IsZero"] = false;

                    // 4. Probability element.
                    JObject probObj = new JObject();
                    probObj["$type"] = "UAssetAPI.PropertyTypes.Objects.FloatPropertyData, UAssetAPI";
                    probObj["Value"] = transition.Probability;
                    probObj["Name"] = "m_fProbability";
                    probObj["DuplicationIndex"] = 0;
                    probObj["IsZero"] = false;
                    

                    // Assemble the value array.
                    valueArray.Add(enumObj);
                    valueArray.Add(condObj);
                    valueArray.Add(targetNodesObj);
                    valueArray.Add(probObj);
                    transitionObj["Value"] = valueArray;
                    transitionEntries.Add(transitionObj);
                    dupIndex++;
                }
            }

            // Create array property enclosing transitions.
            JObject transitionsArrayProperty = new JObject();
            transitionsArrayProperty["$type"] = "UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI";
            transitionsArrayProperty["ArrayType"] = "StructProperty";

            // If there are no transitions, include the dummy struct; otherwise remove it.
            if (transitionEntries.Count == 0)
            {
                JObject dummy = JObject.Parse(@"
                {
                    ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
                    ""StructType"": ""ComboTransition"",
                    ""SerializeNone"": true,
                    ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
                    ""Name"": ""m_Transitions"",
                    ""DuplicationIndex"": 0,
                    ""IsZero"": false,
                    ""Value"": []
                }");
                transitionsArrayProperty["DummyStruct"] = dummy;
            }
            else
            {
                transitionsArrayProperty.Remove("DummyStruct");
            }

            transitionsArrayProperty["Name"] = "m_Transitions";
            transitionsArrayProperty["DuplicationIndex"] = 0;
            transitionsArrayProperty["IsZero"] = false;
            transitionsArrayProperty["Value"] = transitionEntries;

            // Build the m_ParentNodeName element.
            JObject parentNameObj = new JObject();
            parentNameObj["$type"] = "UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI";
            parentNameObj["Name"] = "m_ParentNodeName";
            parentNameObj["DuplicationIndex"] = 0;
            parentNameObj["IsZero"] = false;
            parentNameObj["Value"] = !string.IsNullOrEmpty(node.ParentNodeName) ? node.ParentNodeName : "None";

            // Wrap both parts in a final array.
            JArray transitionsFinalArray = new JArray();
            transitionsFinalArray.Add(transitionsArrayProperty);
            transitionsFinalArray.Add(parentNameObj);

            // Create the container for the transitions block.
            JObject transitionsBlock = new JObject();
            transitionsBlock["$type"] = "UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI";
            transitionsBlock["StructType"] = "ComboTransitions";
            transitionsBlock["SerializeNone"] = true;
            transitionsBlock["StructGUID"] = "{00000000-0000-0000-0000-000000000000}";
            transitionsBlock["Name"] = "m_Transitions";
            transitionsBlock["DuplicationIndex"] = 0;
            transitionsBlock["IsZero"] = false;
            transitionsBlock["Value"] = transitionsFinalArray;

            return transitionsBlock;
        }

        private JObject BuildNodeRedirect(MNode node)
        {
            JObject obj = new JObject();
            obj["$type"] = "UAssetAPI.PropertyTypes.Objects.IntPropertyData, UAssetAPI";
            obj["Name"] = "m_NodeRedirect";
            obj["DuplicationIndex"] = 0;
            obj["IsZero"] = false;
            obj["Value"] = node.NodeRedirect;
            return obj;
        }

        private JObject BuildAvailabilityLayer(MNode node)
        {
            // Return the availability layer block unchanged.
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
              ""StructType"": ""AvailabilityLayerContainer"",
              ""SerializeNone"": true,
              ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
              ""Name"": ""m_NodeRedirectAvailabilityLayer"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": [
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI"",
                  ""Name"": ""m_availabilityLayerData"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": 0
                },
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Objects.EnumPropertyData, UAssetAPI"",
                  ""EnumType"": ""EALBinaryOperation"",
                  ""InnerType"": null,
                  ""Name"": ""m_eOperation"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": ""EALBinaryOperation::SET""
                },
                {
                  ""$type"": ""UAssetAPI.PropertyTypes.Objects.IntPropertyData, UAssetAPI"",
                  ""Name"": ""m_iPriority"",
                  ""DuplicationIndex"": 0,
                  ""IsZero"": false,
                  ""Value"": 0
                }
              ]
            }";
            return JObject.Parse(template);
        }

        private JObject BuildRedirectIgnoredTransitions(MNode node)
        {
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Objects.ArrayPropertyData, UAssetAPI"",
              ""ArrayType"": ""EnumProperty"",
              ""DummyStruct"": null,
              ""Name"": ""m_NodeRedirectIgnoredTransitions"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": []
            }";
            return JObject.Parse(template);
        }

        private JObject BuildEvent(MNode node)
        {
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI"",
              ""Name"": ""m_Event"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": 0
            }";
            return JObject.Parse(template);
        }

        private JObject BuildName(MNode node)
        {
            JObject nameObj = new JObject();
            nameObj["$type"] = "UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI";
            nameObj["Name"] = "m_Name";
            nameObj["DuplicationIndex"] = 0;
            nameObj["IsZero"] = false;
            nameObj["Value"] = node.MNodeName;
            return nameObj;
        }

        private JObject BuildSkip(MNode node)
        {
            JObject obj = new JObject();
            obj["$type"] = "UAssetAPI.PropertyTypes.Objects.BoolPropertyData, UAssetAPI";
            obj["Name"] = "m_bSkip";
            obj["DuplicationIndex"] = 0;
            obj["IsZero"] = false;
            obj["Value"] = node.BSkip;
            return obj;
        }



        


        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            ReplaceWithSelectedMove(selectedComboNode.MNode, MoveSelectorComboBox.Text);
        }

        private void ChangeNodeNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedComboNode == null || selectedComboNode.MNode == null)
            {
                MessageBox.Show("Please select a node in the graph to rename.");
                return;
            }

            string newNodeName = NewNodeNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newNodeName))
            {
                MessageBox.Show("Please enter a valid new node name.");
                return;
            }

            // Check for duplicate names (excluding the current node)
            if (mNodesList.Any(n => n != selectedComboNode.MNode && n.MNodeName.Equals(newNodeName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"A node with the name '{newNodeName}' already exists. Please choose a different name.");
                return;
            }

            // Update the MNode's name
            selectedComboNode.MNode.MNodeName = newNodeName;

            // Update the SelectedNodeTextBlock
            SelectedNodeTextBlock.Text = $"Selected Node: {selectedComboNode.MNode.MNodeName}";

            // Clear the NewNodeNameTextBox
            NewNodeNameTextBox.Clear();

            UpdateGraph();
            var key = comboNodeByUniqueId.FirstOrDefault(x => x.Value.MNode.MNodeName == newNodeName).Key;
            selectedComboNode = FindComboNodeByUniqueId(key);
            FocusOnNodeInGraph(selectedComboNode);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Save Modified JSON File"
            };

            // Show the dialog and if the user clicks OK...
            if (saveFileDialog.ShowDialog() == true)
            {
                // Call our WriteToJSON method with the selected file path.
                WriteToJSON(saveFileDialog.FileName);
                MessageBox.Show("File saved successfully.", "Save JSON", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddTransitionButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedComboNode == null)
            {
                MessageBox.Show("Please select a node in the graph first.");
                return;
            }

            // Get the selected transition type from the ComboBox
            if (TransitionTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a transition type.");
                return;
            }

            EComboTransition transitionType = (EComboTransition)TransitionTypeComboBox.SelectedItem;

            // Get the selected target node
            var targetNodeName = TransitionTargetComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(targetNodeName))
            {
                MessageBox.Show("Please select a target node.");
                return;
            }

            var targetMNode = mNodesList.FirstOrDefault(n => n.MNodeName == targetNodeName);
            if (targetMNode == null)
            {
                MessageBox.Show($"Target node '{targetNodeName}' not found.");
                return;
            }

            // Add a new transition to the selectedMNode
            Transition newTransition = new Transition
            {
                AttackType = transitionType,
                TargetNodeIndices = new List<int>() { targetMNode.IndexInMNodesArray }
            };

            // Add to the selected node's transitions
            selectedComboNode.MNode.Transitions.Add(newTransition);

            var pathToSelectedNode = GetPathToComboNode(selectedComboNode);

            // Regenerate combos and update the graph
            var allCombos = GenerateCombos();
            DisplayCombosGraphMSAGL(allCombos);
            selectedComboNode = FindComboNodeByPath(pathToSelectedNode, allCombos);
            FocusOnNodeInGraph(selectedComboNode);

            MessageBox.Show($"Added new transition '{transitionType}' to node '{selectedComboNode.MNode.MNodeName}' pointing to '{targetNodeName}'.");
        }

        private void CreateNewMNodeButton_Click(object sender, RoutedEventArgs e)
        {
            string newNodeName = NewMNodeNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newNodeName))
            {
                MessageBox.Show("Please enter a name for the new node.");
                return;
            }

            // Check for duplicate names
            if (mNodesList.Any(n => n.MNodeName == newNodeName))
            {
                MessageBox.Show($"A node with the name '{newNodeName}' already exists.");
                return;
            }

            // Create a new MNode
            MNode newMNode = new MNode
            {
                MNodeName = newNodeName,
                IndexInMNodesArray = mNodesList.Count, // Next index
                AttackInfos = new List<AttackInfo>(),
                Transitions = new List<Transition>()
            };
            for (int i = 0; i < 4; i++) newMNode.AttackInfos.Add(new AttackInfo { ExtraPackagePath = "None" });

            mNodesList.Add(newMNode);

            UpdateGraph();
            selectedComboNode = comboTrees.FirstOrDefault(n => n.MNode == newMNode);
            FocusOnNodeInGraph(selectedComboNode);

            MessageBox.Show($"Created new mNode with name '{newMNode.MNodeName}'.");

            // Clear the TextBox after creation
            NewMNodeNameTextBox.Clear();
        }

        private void ImportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void AddExtraMoveButton_Click(object sender, RoutedEventArgs e)
        {
            // Read and trim the user input from the textbox
            string extraMovePath = ExtraMovePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(extraMovePath))
            {
                MessageBox.Show("Please enter an extra move path in the format /path/NewMove.NewMove",
                                "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Validate that the path starts with '/'
            if (!extraMovePath.StartsWith("/"))
            {
                MessageBox.Show("Move path must start with '/'",
                                "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Find the file part after the last '/'
            int lastSlash = extraMovePath.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash >= extraMovePath.Length - 1)
            {
                MessageBox.Show("Invalid move path.",
                                "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Expecting filePart to be like "NewMove.NewMove"
            string filePart = extraMovePath.Substring(lastSlash + 1);
            string[] parts = filePart.Split('.');
            if (parts.Length != 2)
            {
                MessageBox.Show("Move path should be in the format: /path/NewMove1.NewMove1",
                                "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string moveName = parts[0];

            // Check if a MoveEntry with the same move name already exists
            if (moveEntries.Any(entry => string.Equals(entry.Name, moveName, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("A move with that name already exists.",
                                "Duplicate Entry", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Compute package path by removing the dot plus the 2nd part.
            // For example, if extraMovePath is "/path/NewMove.NewMove",
            // then packagePath becomes "/path/NewMove".
            string packagePath = extraMovePath.Substring(0, extraMovePath.Length - (parts[1].Length + 1));

            // Add these three entries to the full name map (our in‑memory List<string> nameMap)
            if (!nameMap.Contains(packagePath))
                nameMap.Add(packagePath);
            if (!nameMap.Contains(extraMovePath))
                nameMap.Add(extraMovePath);
            if (!nameMap.Contains(moveName))
                nameMap.Add(moveName);

            // Also add a corresponding MoveEntry to our UI list (moveEntries)
            MoveEntry newEntry = new MoveEntry
            {
                Name = moveName,
                PackagePath = packagePath,
                ExtraPackagePath = extraMovePath
            };
            moveEntries.Add(newEntry);

            DisplayNameMapEntries(moveEntries);

            MessageBox.Show("New move added successfully.",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetAttackInfoMode(bool isAdvanced)
        {
            // When in advanced mode, show all 4 rows; in simple mode hide rows 2, 3 and 4.
            Visibility extraVisibility = isAdvanced ? Visibility.Visible : Visibility.Collapsed;

            SelectReplaceLabel.Visibility = isAdvanced ? Visibility.Visible : Visibility.Collapsed;

            Attack2CheckBox.Visibility = extraVisibility;
            Attack2TextBox.Visibility = extraVisibility;

            Attack3CheckBox.Visibility = extraVisibility;
            Attack3TextBox.Visibility = extraVisibility;

            Attack4CheckBox.Visibility = extraVisibility;
            Attack4TextBox.Visibility = extraVisibility;

            if (!isAdvanced)
            {
                Attack1CheckBox.Content = "Attack Move";
                Attack1CheckBox.IsChecked = true;

                UpdateSimpleInfosCheckboxes();
            }
            else
            {
                Attack1CheckBox.Content = "Attack Info 1";
            }
        }

        private void UpdateSimpleInfosCheckboxes()
        {
            if (Attack1TextBox.Text == Attack2TextBox.Text)
                Attack2CheckBox.IsChecked = true;
            else
                Attack2CheckBox.IsChecked = false;

            if (Attack1TextBox.Text == Attack3TextBox.Text)
                Attack3CheckBox.IsChecked = true;
            else
                Attack3CheckBox.IsChecked = false;

            if (Attack1TextBox.Text == Attack4TextBox.Text)
                Attack4CheckBox.IsChecked = true;
            else
                Attack4CheckBox.IsChecked = false;
        }

        private void AdvancedModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Advanced mode: show all attack info controls.
            SetAttackInfoMode(true);
        }

        private void AdvancedModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Simple mode: hide extra rows and copy the first row's values.
            SetAttackInfoMode(false);
            //CopyAttack1ToOthers();
        }







        private void GraphViewer_NodeMouseDown(object sender, MsaglMouseEventArgs e)
        {
            if (e.LeftButtonIsPressed)
            {
                var node = graphViewer.ObjectUnderMouseCursor as IViewerNode;
                if (node != null)
                {
                    string uniqueId = node.Node.Id;
                    //var foundNode = FindComboNodeByUniqueId(uniqueId, comboTrees);
                    var foundNode = FindComboNodeByUniqueId(uniqueId);

                    if (foundNode != null)
                    {
                        selectedComboNode = foundNode;
                        DisplayAttackInfos(selectedComboNode.MNode);
                        SelectedNodeTextBlock.Text = $"Selected Node: {selectedComboNode.MNode.MNodeName}";

                        UpdateSimpleInfosCheckboxes();
                        HighlightSimilarNodes(selectedComboNode.MNode.MNodeName);
                    }
                }
            }
        }       

        private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isPanning && (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
            {
                isPanning = true;
                panStartPoint = e.GetPosition(this);
                previousCursor = Cursor;
                Cursor = Cursors.Hand;

                // Capture the mouse to receive events outside the GraphCanvas
                graphViewer.GraphCanvas.CaptureMouse();

                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                var currentPoint = e.GetPosition(this); // Use the window or a parent control

                double deltaX = currentPoint.X - panStartPoint.X;
                double deltaY = currentPoint.Y - panStartPoint.Y;

                // Get or create the MatrixTransform
                var matrixTransform = graphViewer.GraphCanvas.RenderTransform as MatrixTransform;
                if (matrixTransform == null)
                {
                    matrixTransform = new MatrixTransform();
                    graphViewer.GraphCanvas.RenderTransform = matrixTransform;
                }

                Matrix matrix = matrixTransform.Matrix;

                // Apply the translation directly
                matrix.OffsetX += deltaX;
                matrix.OffsetY += deltaY;

                matrixTransform.Matrix = matrix;

                panStartPoint = currentPoint;

                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right))
            {
                isPanning = false;
                Cursor = previousCursor;

                if (graphViewer.GraphCanvas.IsMouseCaptured)
                {
                    graphViewer.GraphCanvas.ReleaseMouseCapture();
                }

                e.Handled = true;
            }
        }

        private void GraphCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                isPanning = false;
                Cursor = previousCursor;

                if (graphViewer.GraphCanvas.IsMouseCaptured)
                {
                    graphViewer.GraphCanvas.ReleaseMouseCapture();
                }

                e.Handled = true;
            }
        }

        private void MainWindow_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right))
            {
                isPanning = false;
                Cursor = previousCursor;

                if (graphViewer.GraphCanvas.IsMouseCaptured)
                {
                    graphViewer.GraphCanvas.ReleaseMouseCapture();
                }

                e.Handled = true;
            }
        }





    }

}
