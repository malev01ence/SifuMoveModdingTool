using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json.Linq;

namespace SifuMoveModdingTool
{
    public class MNode
    {
        public string MNodeName { get; set; }

        public List<AttackInfo> AttackInfos { get; set; } // The attacks inside m_AttackInfos
        public List<Transition> Transitions { get; set; }
        public List<ConditionalAttackEntry> ConditionalAttacks { get; set; }
        public List<string> RequiredTags { get; set; } = new List<string>();
        public List<ChargedAttackBlock> ChargedAttacks { get; set; } = new List<ChargedAttackBlock>();
        public List<BuildUpAttackBlock> BuildUpForChargedAttack { get; set; } = new List<BuildUpAttackBlock>();
        public List<AnimContainer> BuildUpAnimContainers { get; set; } = new List<AnimContainer>();
        public BuildUpGenericInfos BuildUpGenericInfos { get; set; }

        public string ParentNodeName { get; set; }
        public int NodeRedirect {  get; set; }
        public bool BSkip {  get; set; }

        public int IndexInMNodesArray { get; set; } // Index of the m_Node in the m_Nodes array

        public MNode()
        {
            AttackInfos = new List<AttackInfo>();
            Transitions = new List<Transition>();
            ConditionalAttacks = new List<ConditionalAttackEntry>();
            ChargedAttacks = new List<ChargedAttackBlock>();
            BuildUpForChargedAttack = new List<BuildUpAttackBlock>();
        }
    }

    public class AttackInfo
    {
        public string ExtraPackagePath { get; set; }
        public int AttackInfoIndex { get; set; }       // Index in m_AttackInfos
    }

    public class Transition
    {
        public EComboTransition AttackType { get; set; } // E.g., "Heavy", "Light", etc.
        public List<int> TargetNodeIndices { get; set; } = new List<int>(); // All target node indices
        public double Probability { get; set; }
        public int ConditionInstance { get; set; }
        public List<int> TargetKeys { get; set; } = new List<int>();
    }
    
    public class ImportsNode
    {
        public int InnerIndex { get; set; }
        public int OuterIndex { get; set; }
        public int IndexInImportsArray { get; set; } // Index in the Imports array
        public string ObjectName { get; set; }    // Name of the object
        public string ClassPackage { get; set; }
        public string ClassName { get; set; }
    }

    public class ConditionalAttackEntry
    {
        public string TagName { get; set; }
        public List<string> Moves { get; set; } // For each move in this conditional group
    }

    public class ChargedAttackBlock
    {
        // In the original the block holds 4 move names and 4 corresponding start ratios.
        public List<ChargedAttackEntry> Entries { get; set; } = new List<ChargedAttackEntry>();
    }

    public class ChargedAttackEntry
    {
        public string Attack { get; set; }  // e.g. the full package path or “None”
        public string StartRatio { get; set; }  // as written in the file (e.g. "+0")
    }

    public class BuildUpAttackBlock
    {
        public AnimContainer AnimContainer { get; set; }
        public BuildUpGenericInfos Infos { get; set; }
    }

    public class AnimContainer
    {
        // m_animation is stored as an integer
        public int Animation { get; set; }
        public bool Mirror { get; set; }
        public bool Loopable { get; set; }
        // m_fStartRatio is stored as a string (e.g. "+0")
        public string StartRatio { get; set; }
        public double PlayRate { get; set; }
    }

    public class BuildUpGenericInfos
    {
        public double DurationOfCharging { get; set; }
        public double ExtraHoldingTimeAfterChargeIscomplete { get; set; }
        public bool AllowQuadrantToRecompute { get; set; }
    }


    public class MAttacksNode
    {
        public int ValueIndex { get; set; } // e.g., -71 (the value from m_Attacks)
        public string AttackFullPath { get; set; } // The path of the attack
        public int IndexInMAttacksArray { get; set; }
    }

    public class MoveEntry
    {
        public string Name { get; set; }
        public string PackagePath { get; set; }
        public string ExtraPackagePath { get; set; }
    }

    public class ResultDisplay
    {
        public int No { get; set; }            
        public string Category { get; set; } // e.g., "Import", "MNode"
        public string Detail { get; set; }   // e.g., Index information, MNode name
        public string AdditionalInfo { get; set; } // e.g., Transitions
    }

    public class ImportsDisplay : INotifyPropertyChanged
    {
        public int No { get; set; }
        public string MoveName { get; set; }
        public int InnerIndex { get; set; }
        public int OuterIndex { get; set; }

        private bool isHighlighted;

        public bool IsHighlighted
        {
            get { return isHighlighted; }
            set
            {
                if (isHighlighted != value)
                {
                    isHighlighted = value;
                    OnPropertyChanged(nameof(IsHighlighted));
                }
            }
        }

        // Implement INotifyPropertyChanged interface
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }


    public class ComboNode
    {
        public string UniqueId { get; set; }
        public MNode MNode { get; set; }
        public List<ComboTransition> Transitions { get; set; }
        public ComboNode ParentComboNode { get; set; }

        public ComboNode()
        {
            Transitions = new List<ComboTransition>();
        }
    }
    
    public class ComboTransition : Transition
    {
        public ComboNode TransitionNode { get; set; }  // The node itself
        public ComboTransition()
        {
            TransitionNode = new ComboNode();
        }
    }

    public enum EComboTransition
    {
        Light,
        Heavy,
        LightHold,
        HeavyHold,
        HeavyAlt
    }






    public static class TemplateProvider
    {
        // Template for an Import entry.
        private static readonly string ImportTemplateJson = @"
        {
          ""$type"": ""UAssetAPI.Import, UAssetAPI"",
          ""ObjectName"": """",
          ""OuterIndex"": 0,
          ""ClassPackage"": ""/Script/CoreUObject"",
          ""ClassName"": ""Package"",
          ""PackageName"": null,
          ""bImportOptional"": false
        }";

        /// <summary>
        /// Returns a fresh deep clone of the Import template as a JObject.
        /// </summary>
        public static JObject GetNewImportTemplate()
        {
            JObject template = JObject.Parse(ImportTemplateJson);
            return (JObject)template.DeepClone();
        }

        // Template for an MAttacks entry (an array with two elements).
        private static readonly string MAttacksTemplateJson = @"
        [
          {
            ""$type"": ""UAssetAPI.PropertyTypes.Objects.NamePropertyData, UAssetAPI"",
            ""Name"": ""Key"",
            ""DuplicationIndex"": 0,
            ""IsZero"": false,
            ""Value"": """"
          },
          {
            ""$type"": ""UAssetAPI.PropertyTypes.Objects.ObjectPropertyData, UAssetAPI"",
            ""Name"": ""Value"",
            ""DuplicationIndex"": 0,
            ""IsZero"": false,
            ""Value"": 0
          }
        ]";

        /// <summary>
        /// Returns a fresh deep clone of the MAttacks template as a JArray.
        /// </summary>
        public static JArray GetNewMAttacksTemplate()
        {
            JArray templateArray = JArray.Parse(MAttacksTemplateJson);
            return (JArray)templateArray.DeepClone();
        }


        // Template for the entire m_Nodes structure.
        public static JObject GetNewMNodeTemplate()
        {
            string template = @"
            {
              ""$type"": ""UAssetAPI.PropertyTypes.Structs.StructPropertyData, UAssetAPI"",
              ""StructType"": ""ComboNode"",
              ""SerializeNone"": true,
              ""StructGUID"": ""{00000000-0000-0000-0000-000000000000}"",
              ""Name"": ""m_Nodes"",
              ""DuplicationIndex"": 0,
              ""IsZero"": false,
              ""Value"": []
            }";
            return JObject.Parse(template);
        }


    }


}
