using Sandbox.Game.EntityComponents;
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
        readonly string LCDTag = "Conveyors LCD";

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Update100) == UpdateType.Update100) {
                ScanGrid();
                FindConstructions();
                FindConveyorConnections();
                ShowConnectionsWithSprites();
            }
        }

        readonly List<DisplayCoordinator> Screens = new List<DisplayCoordinator>();

        readonly List<IMyTerminalBlock> allInventoryBlocks = new List<IMyTerminalBlock>();
        readonly List<List<IMyTerminalBlock>> constructs = new List<List<IMyTerminalBlock>>();
        readonly List<List<IMyInventory>> smallPortSegments = new List<List<IMyInventory>>();
        // 3 levels of List -> Hard to name well
        // Constructions -> Connected -> Inventories
        readonly List<List<List<IMyInventory>>> inventoryConveyorGroups = new List<List<List<IMyInventory>>>();
        readonly StringBuilder _outputText = new StringBuilder();
        readonly List<IMyTextPanel> MyLCDs = new List<IMyTextPanel>();

        readonly MyItemType LargeItem = MyItemType.MakeComponent("LargeTube");
        bool HasLCDTag(IMyTerminalBlock TermBlock) =>
            TermBlock.CustomName.Contains(LCDTag);

        private void FindLCDs()
        {
            GridTerminalSystem.GetBlocksOfType(MyLCDs, HasLCDTag);

            if (MyLCDs.Count == 0)
            {
                Echo("Warning: Cannot find LCD with group or tag " + LCDTag);
            }
            foreach (var LCD in MyLCDs)
            {
                var displayCoordinator = new DisplayCoordinator(LCD, 0.5f);
                Screens.Add(displayCoordinator);
            }
        }


        private void ScanGrid() {
            FindLCDs();
            GridTerminalSystem.GetBlocksOfType(allInventoryBlocks, block => block.HasInventory);
        }


        private void FindConstructions() {
            constructs.Clear();

            foreach (var block in allInventoryBlocks)
            {
                var found = false;
                foreach (var construct in constructs)
                {
                    if (block.IsSameConstructAs(construct[0])) {
                        construct.Add(block);
                        found = true;
                        break;
                    }

                }

                if (!found)
                {
                    var newConstruct = new List<IMyTerminalBlock> { block };
                    constructs.Add(newConstruct);
                }
            }
        }

        private void FindConveyorConnections()
        {
            inventoryConveyorGroups.Clear();

            foreach (var construct in constructs)
            {
                List<List<IMyInventory>> group = new List<List<IMyInventory>>();
                inventoryConveyorGroups.Add(group);

                foreach (var block in construct)
                {
                    var blockInventory = block.GetInventory();
                    var found = false;
                    foreach (List<IMyInventory> groupInventory in group)
                    {
                        foreach (IMyInventory inv in groupInventory)
                        {
                            if (blockInventory.IsConnectedTo(inv))
                            {
                                groupInventory.Add(blockInventory);

                                found = true;
                                break;
                            }
                        }
                       if (found) break;
                    }

                    if (!found)
                    {
                        var newGroupInventory = new List<IMyInventory> { blockInventory };
                        group.Add(newGroupInventory);
                    }
                }
            }
        }

        private void ShowConnectionsAsText()
        {
            _outputText.Clear();
            _outputText.Append("List of constructions:\n---------------------\n");
            _outputText.AppendLine();

            foreach (List<List<IMyInventory>> group in inventoryConveyorGroups)
            {
                _outputText.Append("Grid: ");
                IMyCubeGrid thisGrid = ((IMyTerminalBlock)group[0][0].Owner).CubeGrid;
                _outputText.AppendLine(thisGrid.DisplayName);
                MyCubeSize gridSize =  thisGrid.GridSizeEnum;
                //if (group.Count == 1 && gridSize == MyCubeSize.Large)
                //{
                //    _outputText.AppendLine("  Connected.");
                //}
                //else
                {
                    foreach (List<IMyInventory> groupInventory in group)
                    {
                        //if (gridSize == MyCubeSize.Large)
                        //{
                        
                        //    foreach (IMyInventory inv in groupInventory)
                        //    {
                        //        _outputText.Append("  ||  ");
                        //        _outputText.AppendLine(((IMyCubeBlock)inv.Owner).DisplayNameText); 
                        //    }
                        
                        //}
                        //else
                        {
                            smallPortSegments.Clear();
                            foreach (IMyInventory inv in groupInventory)
                            {
                                bool found = false;
                                foreach (List<IMyInventory> segment in smallPortSegments)
                                {
                                    // Detect large or small port connection
                                    bool hasLargePort = inv.CanTransferItemTo(segment[0], LargeItem);
                                    if (hasLargePort)
                                    {
                                        segment.Add(inv);
                                        IMyCubeBlock block = inv.Owner as IMyCubeBlock;
                                        Echo(block.DefinitionDisplayNameText);
                                        Echo(((IMyCubeBlock)inv.Owner).DisplayNameText);

                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    smallPortSegments.Add(new List<IMyInventory> { inv });
                                }
                            }
                            foreach (var segment in smallPortSegments)
                            {
                                foreach (var inv in segment)
                                {
                                    _outputText.Append("    ||  ");
                                    _outputText.AppendLine(((IMyCubeBlock)inv.Owner).DisplayNameText);

                                }
                                _outputText.AppendLine("     |  ");
                            }
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

        enum ConveyorPortCapacity { none, small, large };

        private void DrawConnectionSprite(ConveyorPortCapacity upwardsConnection, ConveyorPortCapacity downwardsConnection)
        {
            switch (upwardsConnection)
            {
                case ConveyorPortCapacity.large:
                    switch (downwardsConnection)
                    {
                        case ConveyorPortCapacity.none:
                            break;
                        case ConveyorPortCapacity.small:
                            break;
                        case ConveyorPortCapacity.large:
                            break;
                        default:
                            break;
                    }
                    break;

                case ConveyorPortCapacity.small:
                    switch (downwardsConnection)
                    {
                        case ConveyorPortCapacity.none:
                            break;
                        case ConveyorPortCapacity.small:
                            break;
                        case ConveyorPortCapacity.large:
                            break;
                        default:
                            break;
                    }
                    break;

                case ConveyorPortCapacity.none:
                    switch (downwardsConnection)
                    {
                        case ConveyorPortCapacity.none:
                            break;
                        case ConveyorPortCapacity.small:
                            break;
                        case ConveyorPortCapacity.large:
                            break;
                        default:
                            break;
                    }
                    break;

                default:
                    break;
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
            
            //foreach (IMyTextPanel LCD in MyLCDs)
            //{
            //    var Position = new Vector2(0f, 0f);
            //    var frame = LCD.DrawFrame();
            //    LCD.ContentType = ContentType.SCRIPT;

            //    DrawLargePortSprites(frame, new Vector2(0f, 00f));
            //    DrawLargePortSprites(frame, new Vector2(0f, 24f));
            //    DrawLargePortSprites(frame, new Vector2(0f, 48f));
            //    frame.Dispose();
            //}
            foreach(DisplayCoordinator display in Screens)
            {
                display.Render(inventoryConveyorGroups);
            }
        }

    }
}
