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
    partial class Program
    {
        readonly static bool DEBUG = false;
        public class DisplayCoordinator
        // Copied and freely adapted from 
        // https://github.com/Brianetta/SE-All-My-Stuff/blob/main/All%20My%20Stuff/ManagedDisplay.cs
        {
            private IMyTextSurface surface;
            private RectangleF viewport;
            private MySpriteDrawFrame frame;
            private float StartHeight = 0f;
            private float HeadingHeight = 35f;
            private float LineHeight = 24f;
            private float HeadingFontSize = 1.3f;
            private float RegularFontSize = 1.0f;
            private float SpriteScale = 1.0f;
            private Vector2 Position;
            private int WindowSize;         // Number of lines shown on screen at once after heading
            private Color HighlightColor;
            private int linesToSkip;
            private bool monospace;
            private string Heading = "Conveyor Network";
            private bool MakeSpriteCacheDirty = false;

            bool IncludeFullyConnected = true;
            private Color BackgroundColor, ForegroundColor;
            readonly List<List<IMyInventory>> smallPortSegments = new List<List<IMyInventory>>();
            readonly MyItemType LargeItem = MyItemType.MakeComponent("LargeTube");


            public DisplayCoordinator(
                IMyTextSurface surface,
                float scale = 1.0f,
                Color highlightColor = new Color(),
                int linesToSkip = 0, bool monospace = false, bool includeFullyConnected = false)
            {
                this.surface = surface;
                this.HighlightColor = highlightColor;
                this.linesToSkip = linesToSkip;
                this.monospace = monospace;
                this.IncludeFullyConnected = includeFullyConnected;
                this.BackgroundColor = surface.ScriptBackgroundColor;
                this.ForegroundColor = surface.ScriptForegroundColor;

                // Scale everything!
                StartHeight *= scale;
                HeadingHeight *= scale;
                LineHeight *= scale;
                HeadingFontSize *= scale;
                RegularFontSize *= scale;
                SpriteScale *= scale;


                surface.ContentType = ContentType.SCRIPT;
                surface.Script = "TSS_FactionIcon";
                Vector2 padding = surface.TextureSize * (surface.TextPadding / 100);
                viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f + padding, surface.SurfaceSize - (2 * padding));
                WindowSize = ((int)((viewport.Height - 10 * scale) / LineHeight));

            }


            private void AddHeading()
            {
                float finalColumnWidth = HeadingFontSize * 80;
                // that thing above is rough - this is just used to stop headings colliding, nothing serious,
                // and is way cheaper than allocating a StringBuilder and measuring the width of the final
                // column heading text in pixels.
                if (surface.Script != "")
                {
                    surface.Script = "";
                    surface.ScriptBackgroundColor = BackgroundColor;
                    surface.ScriptForegroundColor = ForegroundColor;
                }
                Position = new Vector2(viewport.Width / 10f, StartHeight) + viewport.Position;
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "Textures\\FactionLogo\\Builders\\BuilderIcon_2.dds",
                    Position = Position + new Vector2(0f, LineHeight / 2f),
                    Size = new Vector2(LineHeight, LineHeight),
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.CENTER
                });
                Position.X += viewport.Width / 8f;
                frame.Add(MySprite.CreateClipRect(new Rectangle((int)Position.X, (int)Position.Y, (int)(viewport.Width - Position.X - finalColumnWidth), (int)(Position.Y + HeadingHeight))));
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = Heading,
                    Position = Position,
                    RotationOrScale = HeadingFontSize,
                    Color = HighlightColor,
                    Alignment = TextAlignment.LEFT,
                    FontId = "White"
                });
                frame.Add(MySprite.CreateClearClipRect());
                Position.Y += HeadingHeight;

            }
            private void DrawConnectionSprite(ConveyorPortCapacity upwardsConnection, ConveyorPortCapacity downwardsConnection)
            {
                switch (upwardsConnection)
                {
                    case ConveyorPortCapacity.large:
                        switch (downwardsConnection)
                        {
                            case ConveyorPortCapacity.none:
                                ConnectionLargeNone();
                                break;
                            case ConveyorPortCapacity.small:
                                ConnectionLargeSmall();
                                break;
                            case ConveyorPortCapacity.large:
                                ConnectionLargeLarge();
                                break;
                            default:
                                break;
                        }
                        break;

                    case ConveyorPortCapacity.small:
                        switch (downwardsConnection)
                        {
                            case ConveyorPortCapacity.none:
                                ConnectionSmallNone();
                                break;
                            case ConveyorPortCapacity.small:
                                ConnectionSmallSmall();
                                break;
                            case ConveyorPortCapacity.large:
                                ConnectionSmallLarge();
                                break;
                            default:
                                break;
                        }
                        break;

                    case ConveyorPortCapacity.none:
                        switch (downwardsConnection)
                        {
                            case ConveyorPortCapacity.none:
                                // Blank
                                break;
                            case ConveyorPortCapacity.small:
                                ConnectionNoneSmall();
                                break;
                            case ConveyorPortCapacity.large:
                                ConnectionNoneLarge();
                                break;
                            default:
                                break;
                        }
                        break;

                    default:
                        break;
                }

            }

            private void ConnectionLargeLarge()
            {
                DrawRectangleAt(17f, 09f, 08f, 02f); // rightTopHorizontal
                DrawRectangleAt(17f, 17f, 08f, 02f); // rightLowHorizontal
                DrawRectangleAt(14f, 05f, 02f, 08f); // rightTopVertical
                DrawRectangleAt(14f, 21f, 02f, 08f); // rightLowVertical
                DrawRectangleAt(06f, 13f, 02f, 24f); // leftVertical
            }
            private void ConnectionLargeSmall()
            {
                DrawRectangleAt(10f, 21f, 02f, 08f); // LowVertical
                DrawRectangleAt(14f, 17f, 14f, 02f); // rightLowHorizontal
                DrawRectangleAt(17f, 09f, 08f, 02f); // rightTopHorizontal
                DrawRectangleAt(14f, 05f, 02f, 08f); // rightLowVertical
                DrawRectangleAt(06f, 09f, 02f, 18f); // leftVertical
            }
            private void ConnectionSmallLarge()
            {
                DrawRectangleAt(10f, 05f, 02f, 08f); // TopVertical
                DrawRectangleAt(13f, 09f, 16f, 02f); // rightTopHorizontal
                DrawRectangleAt(14f, 21f, 02f, 08f); // rightLowVertical
                DrawRectangleAt(17f, 17f, 08f, 02f); // rightLowHorizontal
                DrawRectangleAt(06f, 17f, 02f, 17f); // leftVertical
            }

            private void ConnectionNoneLarge()
            {
                DrawRectangleAt(13f, 09f, 16f, 02f); // rightTopHorizontal
                DrawRectangleAt(14f, 21f, 02f, 08f); // rightLowVertical
                DrawRectangleAt(17f, 17f, 08f, 02f); // rightLowHorizontal
                DrawRectangleAt(06f, 17f, 02f, 17f); // leftVertical
            }

            private void ConnectionLargeNone()
            {
                DrawRectangleAt(14f, 17f, 14f, 02f); // rightLowHorizontal
                DrawRectangleAt(14f, 05f, 02f, 08f); // rightTopVertical
                DrawRectangleAt(17f, 09f, 08f, 02f); // rightTopHorizontal
                DrawRectangleAt(06f, 09f, 02f, 17f); // leftVertical
            }
            private void ConnectionSmallSmall()
            {
                DrawRectangleAt(10f, 13f, 02f, 24f); // centerVertical
                DrawRectangleAt(15f, 13f, 12f, 02f); // centerHorizontal
            }
            private void ConnectionSmallNone()
            {
                DrawRectangleAt(10f, 07f, 02f, 13f); // centerVertical
                DrawRectangleAt(15f, 13f, 12f, 02f); // centerHorizontal
            }
            private void ConnectionNoneSmall()
            {
                DrawRectangleAt(10f, 19f, 02f, 12f); // centerVertical
                DrawRectangleAt(15f, 13f, 12f, 02f); // centerHorizontal
            }

            private void DrawRectangleAt(float x0, float y0, float x1, float y1)
            {
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "SquareSimple",
                    Position = new Vector2(x0, y0) * SpriteScale + Position,
                    Size = new Vector2(x1, y1) * SpriteScale,
                    Color = ForegroundColor,
                    RotationOrScale = 0f
                });
            }

            internal void Render(List<List<List<IMyInventory>>> inventoryConveyorGroups)
            {
                frame = surface.DrawFrame();
                ToggleSpriteCache();
                if (DEBUG) {
                    DebugConveyorSizes();
                    frame.Dispose();
                    return;
                } 
                
                AddHeading();
                int renderLineCount = 0;
                foreach (List<List<IMyInventory>> group in inventoryConveyorGroups)
                {
                    IMyCubeGrid thisGrid = ((IMyTerminalBlock)group[0][0].Owner).CubeGrid;
                    MyCubeSize gridSize = thisGrid.GridSizeEnum;
                    Position.X = viewport.Width / 32f + viewport.Position.X;
                    DrawGridName(group);
                    renderLineCount++;
                    Position.Y += LineHeight;
                    foreach (var groupInventory in group)
                    {
                        FindSmallPortSegments(groupInventory);
                        for (int segmentIndex = 0; segmentIndex < smallPortSegments.Count; segmentIndex++)
                        {
                            List<IMyInventory> segment = smallPortSegments[segmentIndex];
                            for (int inventoryIndex = 0; inventoryIndex < segment.Count; inventoryIndex++)
                            {
                                IMyInventory inv = segment[inventoryIndex];
                                Position.X = viewport.Width / 16f + viewport.Position.X;
                                DrawConveyorSize(segmentIndex, segment, inventoryIndex, inv);
                                renderLineCount++;
                                Position.Y += LineHeight;
                            }
                        }
                    }
                }
                frame.Dispose();
            }

            private void DebugConveyorSizes()
            {
                for (var upwards = ConveyorPortCapacity.none; upwards <= ConveyorPortCapacity.large; upwards++)
                {
                    for (var downwards = ConveyorPortCapacity.none; downwards <= ConveyorPortCapacity.large; downwards++)
                    {
                        Position.X = viewport.Width / 16f + viewport.Position.X;
                        DrawConnectionSprite(upwards, downwards);
                        frame.Add(new MySprite()
                        {
                            Type = SpriteType.TEXT,
                            Alignment = TextAlignment.LEFT,
                            Data = upwards.ToString() + ' '+ downwards.ToString(), 
                            Position = new Vector2(32f, -5f) * RegularFontSize + Position,
                            Color = ForegroundColor,
                            FontId = "DEBUG",
                            RotationOrScale = RegularFontSize,
                        }); // Text
                        Position.Y += LineHeight;
                    }
                }
            }

            private void DrawConveyorSize(int segmentIndex, List<IMyInventory> segment, int inventoryIndex, IMyInventory inv)
            {
                ConveyorPortCapacity upwards, downwards;
                if (inventoryIndex == 0)
                {
                    if (segmentIndex == 0)
                    {
                        upwards = ConveyorPortCapacity.none;
                    }
                    else upwards = ConveyorPortCapacity.small;
                }
                else upwards = ConveyorPortCapacity.large;

                if (inventoryIndex == segment.Count - 1)
                {
                    if (segmentIndex == smallPortSegments.Count - 1)
                    {
                        downwards = ConveyorPortCapacity.none;
                    }
                    else downwards = ConveyorPortCapacity.small;
                }
                else downwards = ConveyorPortCapacity.large;

                DrawConnectionSprite(upwards, downwards);
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = ((IMyCubeBlock)inv.Owner).DisplayNameText,
                    Position = new Vector2(32f, -5f) * RegularFontSize + Position,
                    Color = ForegroundColor,
                    FontId = "DEBUG",
                    RotationOrScale = RegularFontSize,
                }); // Text
            }

            private void FindSmallPortSegments(List<IMyInventory> groupInventory)
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

                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        smallPortSegments.Add(new List<IMyInventory> { inv });
                    }
                }
            }

            private void DrawGridName(List<List<IMyInventory>> group)
            {
                frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = ((IMyTerminalBlock)group[0][0].Owner).CubeGrid.DisplayName,
                    Position = new Vector2(32f, -5f) * RegularFontSize + Position,
                    Color = ForegroundColor,
                    FontId = "DEBUG",
                    RotationOrScale = RegularFontSize,
                }); // Text
            }

            private void ToggleSpriteCache()
            {
                MakeSpriteCacheDirty = !MakeSpriteCacheDirty;
                if (MakeSpriteCacheDirty)
                {
                    frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Color = surface.BackgroundColor,
                        Position = new Vector2(0, 0),
                        Size = new Vector2(0, 0)
                    });
                }
            }
        }
    }
}
