﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private const string LCDTag = "Conveyors LCD";
        private const string ConfigSection = "Conveyor";
        private const string DisplaySectionPrefix = ConfigSection + "_Display";
        private const string defaultColorHexString = "FF4500";
        private const string colorKeyName = "color";
        private const string scaleKeyName = "scale";
        private const string skipKeyName = "skip";
        private const string monoKeyName = "mono";
        private const string showAllKeyName = "show_all";
        private const string viaKeyName = "via";
        private const string displayKeyName = "display";
        private readonly StringBuilder SectionCandidateName = new StringBuilder();
        private readonly List<string> SectionNames = new List<string>();
        private readonly MyIni ini = new MyIni();
        private readonly List<DisplayCoordinator> screens = new List<DisplayCoordinator>();
        private readonly List<IMyTerminalBlock> providers = new List<IMyTerminalBlock>();
        private readonly List<IMyTextPanel> MyLCDs = new List<IMyTextPanel>();
        private readonly StringBuilder _outputText = new StringBuilder();

        private readonly List<IMyTerminalBlock> allInventoryBlocks = new List<IMyTerminalBlock>();
        private readonly List<IMyShipConnector> myConnectors = new List<IMyShipConnector>();
        private readonly List<IMyShipConnector> allConnectors = new List<IMyShipConnector>();
        private readonly List<Construct> Constructs = new List<Construct>();


        static readonly MyItemType LargeItem = MyItemType.MakeComponent("LargeTube");
        bool HasLCDTag(IMyTerminalBlock TermBlock) =>
            TermBlock.CustomName.Contains(LCDTag);

        // Caches for List reuse. Separate Lists for each type to avoid extra enumerators generated by Queue.OfType<>
        private static readonly Queue Island_ListQueue = new Queue(); // List<Island>
        private static readonly Queue Segment_ListQueue = new Queue(); // List<Segment>
        private static readonly Queue MTB_ListQueue = new Queue(); // List<IMyTerminaBlock>

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update100) != 0)
            {
                ScanGrid();
                FindConstructions();
                ShowConnectionsAsText();
                ShowConnectionsWithSprites();
            }
        }

        private void ScanGrid()
        {
            screens.Clear();
            allInventoryBlocks.Clear();
            myConnectors.Clear();
            allConnectors.Clear();
            providers.Clear();
            MyLCDs.Clear();

            GridTerminalSystem.GetBlocksOfType(allInventoryBlocks, block =>
            {
                IMyShipConnector connector = block as IMyShipConnector;
                if (connector != null)
                {
                    allConnectors.Add(connector);
                    if (block.IsSameConstructAs(Me))
                        myConnectors.Add(connector);
                }

                IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
                if (provider != null && 
                    provider.SurfaceCount > 0 && 
                    block.IsSameConstructAs(Me))
                {
                    if (HasLCDTag(block))
                    {
                        MyLCDs.Add(block as IMyTextPanel);
                    }
                    else providers.Add(block);
                }
                return block.HasInventory;
            });
            providers.ForEach(displayBlock =>
            {
                if (!TryAddDiscreteScreens(displayBlock))
                {
                    TryAddScreen(displayBlock);
                }
            });
        }

        void AddScreen(IMyTextSurfaceProvider provider, int displayNumber, string section)
        {
            var display = ((IMyTextSurfaceProvider)provider).GetSurface(displayNumber);
            float scale = ini.Get(section, scaleKeyName).ToSingle(0.5f);
            string ColorStr = ini.Get(section, colorKeyName).ToString(defaultColorHexString);
            if (ColorStr.Length < 6)
                ColorStr = defaultColorHexString;
            Color color = new Color()
            {
                R = byte.Parse(ColorStr.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                G = byte.Parse(ColorStr.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                B = byte.Parse(ColorStr.Substring(4, 2), System.Globalization.NumberStyles.HexNumber),
                A = 255
            };
            var linesToSkip = ini.Get(section, skipKeyName).ToInt16();
            bool showAll = ini.Get(section, showAllKeyName).ToBoolean();
            string connectorName = ini.Get(section, viaKeyName).ToString();
            IMyShipConnector viaConnector = null;
            if(connectorName != "")
            {
                viaConnector = myConnectors.Find(con => con.DisplayNameText.Contains(connectorName));
                if (viaConnector == null) Echo($"** WARNING ** '{connectorName}' not found. Full list displayed.");
            }

            screens.Add(new DisplayCoordinator(
                display, scale, color, linesToSkip, showAll, viaConnector));
        }

        private bool TryAddDiscreteScreens(IMyTerminalBlock block)
        {
            bool retval = false;
            IMyTextSurfaceProvider Provider = block as IMyTextSurfaceProvider;
            if (null == Provider || Provider.SurfaceCount == 0)
                return true;
            StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            bool success = ini.TryParse(block.CustomData);
            if (!success)
            {
                Echo($"Warning: Failed to parse custom data in {block.DisplayNameText}");
            }
            ini.GetSections(SectionNames);
            foreach (var section in SectionNames)
            {
                if (section.StartsWith(DisplaySectionPrefix, ignoreCase))
                {
                    for (int displayNumber = 0; displayNumber < Provider.SurfaceCount; ++displayNumber)
                    {
                        SectionCandidateName.Clear();
                        SectionCandidateName.Append(DisplaySectionPrefix).Append(displayNumber.ToString());
                        if (section.Equals(SectionCandidateName.ToString(), ignoreCase))
                        {
                            AddScreen(Provider, displayNumber, section);
                            retval = true;
                        }
                    }
                }
            }
            return retval;
        }

        private void TryAddScreen(IMyTerminalBlock block)
        {
            IMyTextSurfaceProvider Provider = block as IMyTextSurfaceProvider;
            if (null == Provider || Provider.SurfaceCount == 0 || !MyIni.HasSection(block.CustomData, ConfigSection))
                return;
            bool success = ini.TryParse(block.CustomData);
            if (!success)
            {
                Echo($"Warning: Failed to parse custom data in {block.DisplayNameText}");
            }
            var displayNumber = ini.Get(ConfigSection, displayKeyName).ToUInt16();
            if (displayNumber < Provider.SurfaceCount)
            {
                AddScreen(Provider, displayNumber, ConfigSection);
            }
            else
            {
                Echo($"Warning: {block.CustomName} doesn't have a display number {ini.Get(ConfigSection, displayKeyName)}");
            }
        }


        internal class Construct
        {
            public List<Island> Islands { get; private set; }
            public Construct(IMyTerminalBlock newBlock)
            {
                if (Island_ListQueue.Count > 0)
                {
                    var newList = Island_ListQueue.Dequeue() as List<Island>;
                    if (newList != null) Islands = newList;
                }
                if (Islands == null) Islands = new List<Island>();
                var newSegment = new Segment(newBlock);
                var newIsland = new Island(newSegment);
                Islands.Add(newIsland);
            }
            public bool IsSameConstructAs(IMyTerminalBlock block)
            {
                return Islands[0].Segments[0].Blocks[0].IsSameConstructAs(block);
            }
            public void AddBlock(IMyTerminalBlock block)
            {
                bool found = false;
                foreach (var island in Islands)
                {
                    if (island.IsSameIslandAs(block))
                    {
                        island.AddBlock(block);
                        found = true;
                        break;
                    }
                }
                if (!found) Islands.Add(new Island(new Segment(block)));
            }
            public void Clear()
            {
                foreach (var island in Islands)
                {
                    island.Clear();
                }
                Islands.Clear();
                Island_ListQueue.Enqueue(Islands);
                Islands = null;
            }
        }

        internal class Island
        {
            public List<Segment> Segments { get; private set; } = null;
            public Island(Segment newSegment)
            {
                if (Segment_ListQueue.Count > 0)
                {
                    var newList = Segment_ListQueue.Dequeue() as List<Segment>;
                    if (newList != null) Segments = newList;
                }
                if (Segments == null) Segments = new List<Segment>();
                Segments.Add(newSegment);
            }
            public bool IsSameIslandAs(IMyTerminalBlock block)
            {
                return Segments[0].Blocks[0].GetInventory().IsConnectedTo(block.GetInventory());
            }
            public void AddBlock(IMyTerminalBlock block)
            {
                bool found = false;
                foreach (var segment in Segments)
                {
                    if (segment.SameSegment(block))
                    {
                        segment.Blocks.Add(block);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Segments.Add(new Segment(block));
                }
            }
            public void Clear()
            {
                foreach (var segment in Segments)
                {
                    segment.Clear();
                }
                Segments.Clear();
                Segment_ListQueue.Enqueue(Segments);
                Segments = null;
            }
        }

        internal class Segment
        {
            public List<IMyTerminalBlock> Blocks { get; private set; } = null;
            public Segment(IMyTerminalBlock newBlock)
            {
                if (MTB_ListQueue.Count > 0)
                {
                    List<IMyTerminalBlock> newList = MTB_ListQueue.Dequeue() as List<IMyTerminalBlock>;
                    if (newList != null)
                        Blocks = newList;
                }
                if (Blocks == null)
                    Blocks = new List<IMyTerminalBlock>();
                Blocks.Add(newBlock);
            }
            public bool SameSegment(IMyTerminalBlock block)
            {
                return Blocks[0].GetInventory().CanTransferItemTo(block.GetInventory(), LargeItem);
            }
            public void Clear()
            {
                Blocks.Clear();
                MTB_ListQueue.Enqueue(Blocks);
                Blocks = null;
            }
        }

        /// <summary>
        /// constructs filled with all blocks from each construct
        /// </summary>
        private void FindConstructions()
        {
            Constructs.Clear();

            foreach (var block in allInventoryBlocks)
            {
                var found = false;
                foreach (var construct in Constructs)
                {
                    if (construct.IsSameConstructAs(block))
                    {
                        construct.AddBlock(block);
                        found = true;
                        break;
                    }

                }

                if (!found)
                {
                    Constructs.Add(new Construct(block));
                }
            }
        }

        private void ShowConnectionsAsText()
        {
            _outputText.Clear();
            _outputText.Append("List of constructions:\n---------------------\n");
            _outputText.AppendLine();

            foreach (var construct in   Constructs)
            {
                _outputText.Append("Grid: ");
                IMyCubeGrid thisGrid = construct.Islands[0].Segments[0].Blocks[0].CubeGrid;
                _outputText.AppendLine(thisGrid.DisplayName);
                MyCubeSize gridSize = thisGrid.GridSizeEnum;
                //if (group.Count == 1 && gridSize == MyCubeSize.Large)
                //{
                //    _outputText.AppendLine("  Connected.");
                //}
                //else
                {
                    foreach (var island in construct.Islands)
                    {
                        foreach (var segment in island.Segments)
                        {
                            foreach (var block in segment.Blocks)
                            {
                                _outputText.Append("    ||  ");
                                _outputText.AppendLine(block.DisplayNameText);

                            }
                            _outputText.AppendLine("     |  ");
                        }
                        _outputText.AppendLine();
                    }
                    _outputText.AppendLine();
                }
            }


            foreach (IMyTextPanel LCD in MyLCDs)
            {
                LCD.ContentType = ContentType.TEXT_AND_IMAGE;
                LCD.WriteText(_outputText);
            }
        }

        // '╔'
        // '╠'
        private void DrawLargePortSprites(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
        {
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(17f, 9f) * scale + centerPos,
                Size = new Vector2(8f, 2f) * scale,
                Color = new Color(255, 255, 255, 255),
                RotationOrScale = 0f
            }); // rightUpperHorizontal
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(17f, 17f) * scale + centerPos,
                Size = new Vector2(8f, 2f) * scale,
                Color = new Color(255, 255, 255, 255),
                RotationOrScale = 0f
            }); // rifhtLowHorizontal
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(14f, 5f) * scale + centerPos,
                Size = new Vector2(2f, 8f) * scale,
                Color = new Color(255, 255, 255, 255),
                RotationOrScale = 0f
            }); // rightTopVertical
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(14f, 21f) * scale + centerPos,
                Size = new Vector2(2f, 8f) * scale,
                Color = new Color(255, 255, 255, 255),
                RotationOrScale = 0f
            }); // rightLowVertical
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = "SquareSimple",
                Position = new Vector2(6f, 13f) * scale + centerPos,
                Size = new Vector2(2f, 24f) * scale,
                Color = new Color(255, 255, 255, 255),
                RotationOrScale = 0f
            }); // leftVertical
            frame.Add(new MySprite()
            {
                Type = SpriteType.TEXT,
                Alignment = TextAlignment.LEFT,
                Data = "Large Port",
                Position = new Vector2(32f, -5f) * scale + centerPos,
                Color = new Color(255, 255, 255, 255),
                FontId = "DEBUG",
                RotationOrScale = 1f * scale
            }); // Text
        }
        private void ShowConnectionsWithSprites()
        {
            foreach (DisplayCoordinator display in screens)
            {
                display.Render(Constructs);
            }
        }
    }
}
