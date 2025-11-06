using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace BetrayalHelperRevamped
{
    public class BetrayalHelperRevampedSettings : ISettings
    {


        public ToggleNode DebugMode { get; set; }
        public ToggleNode MegaDebugMode { get; set; }
        public ButtonNode ReloadRules { get; set; } = new ButtonNode();




        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        public BetrayalHelperRevampedSettings()
        {
            DebugMode = new ToggleNode(false);
            MegaDebugMode = new ToggleNode(false);
      
           

        }
    }

}