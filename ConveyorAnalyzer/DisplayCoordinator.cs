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
            private readonly string _heading = "Conveyor Network";
            private MySpriteDrawFrame _frame;
            private Vector2 _position;
            private bool _makeSpriteCacheDirty = false;
            private readonly IMyTextSurface _surface;
            private readonly RectangleF _viewport;
            private readonly float _startHeight = 0f;
            private readonly float _headingHeight = 35f;
            private readonly float _lineHeight = 24f;
            private readonly float _headingFontSize = 1.3f;
            private readonly float _regularFontSize = 1.0f;
            private readonly float _spriteScale = 1.0f;
            private readonly int _windowSize;         // Number of lines shown on screen at once after heading
            private readonly Color _highlightColor;
            private readonly int _linesToSkip;
            private readonly bool _includeFullyConnected;
            private readonly IMyShipConnector _viaConnector;
            private readonly Color _backgroundColor, _foregroundColor;

            public DisplayCoordinator(
                IMyTextSurface surface, float scale = 1.0f, Color highlightColor = new Color(),
                int linesToSkip = 0, bool includeFullyConnected = false, IMyShipConnector viaConnector = null)
            {
                this._surface = surface;
                this._highlightColor = highlightColor;
                this._linesToSkip = linesToSkip;
                this._viaConnector = viaConnector;
                this._includeFullyConnected = includeFullyConnected;
                this._backgroundColor = surface.ScriptBackgroundColor;
                this._foregroundColor = surface.ScriptForegroundColor;

                // Scale everything!
                _startHeight *= scale;
                _headingHeight *= scale;
                _lineHeight *= scale;
                _headingFontSize *= scale;
                _regularFontSize *= scale;
                _spriteScale *= scale;


                surface.ContentType = ContentType.SCRIPT;
                // surface.Script = "TSS_FactionIcon";
                Vector2 padding = surface.TextureSize * (surface.TextPadding / 100);
                _viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f + padding, surface.SurfaceSize - (2 * padding));
                _windowSize = ((int)((_viewport.Height - (linesToSkip>0 ? 0 : _headingHeight)) / _lineHeight))  ;

            }


            private void AddHeading(string message = "")
            {
                float finalColumnWidth = _headingFontSize * 40;
                // that thing above is rough - this is just used to stop headings colliding, nothing serious,
                // and is way cheaper than allocating a StringBuilder and measuring the width of the final
                // column heading text in pixels.
                if (_surface.Script != "")
                {
                    _surface.Script = "";
                    _surface.ScriptBackgroundColor = _backgroundColor;
                    _surface.ScriptForegroundColor = _foregroundColor;
                }
                _position = new Vector2(_viewport.Width / 16f, _startHeight) + _viewport.Position;
                if (_linesToSkip > 0)   
                {
                    return;
                }
                _frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "Textures\\FactionLogo\\Builders\\BuilderIcon_2.dds",
                    Position = _position + new Vector2(0f, _lineHeight / 2f),
                    Size = new Vector2(_lineHeight, _lineHeight),
                    RotationOrScale = 0f,
                    Color = _highlightColor,
                    Alignment = TextAlignment.CENTER
                });
                _position.X += _viewport.Width / 16f;
                _frame.Add(MySprite.CreateClipRect(new Rectangle(
                    (int)_position.X, (int)_position.Y, 
                    (int)(_viewport.Width - _position.X - finalColumnWidth), (int)(_position.Y + _headingHeight))));
                _frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = $"{_heading} {message}",
                    Position = _position,
                    RotationOrScale = _headingFontSize,
                    Color = _highlightColor,
                    Alignment = TextAlignment.LEFT,
                    FontId = "White"
                });
                _frame.Add(MySprite.CreateClearClipRect());
                _position.Y += _headingHeight;

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
                _frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "SquareSimple",
                    Position = new Vector2(x0, y0) * _spriteScale + _position,
                    Size = new Vector2(x1, y1) * _spriteScale,
                    Color = _foregroundColor,
                    RotationOrScale = 0f
                });
            }

            internal void Render(List<Construct> constructs)
            {
                Construct selectedConstruct = null;
                _frame = _surface.DrawFrame();
                ToggleSpriteCache();
                if (DEBUG)
                {
                    DebugConveyorSizes();
                    _frame.Dispose();
                    return;
                }
                if (_viaConnector != null)
                {
                    if (_viaConnector.Status == MyShipConnectorStatus.Connected)
                    {
                        IMyShipConnector connector = _viaConnector.OtherConnector;
                        selectedConstruct = constructs.Find(construct => construct.IsSameConstructAs(connector));
                        AddHeading($"via {_viaConnector.DisplayNameText}");
                    }
                    else
                    {
                        AddHeading($"Empty Connector '{_viaConnector.DisplayNameText}'");
                        _frame.Dispose();
                        return;
                    }
                }
                else AddHeading();


                RenderConstructs(constructs, selectedConstruct);
                _frame.Dispose();
            }

            private int RenderLine(int renderLineCount, Action drawLine)
            {
                if (renderLineCount < _windowSize + _linesToSkip
                    && ++renderLineCount > _linesToSkip)
                    drawLine();
                return renderLineCount;
            }

            private void RenderConstructs(List<Construct> constructs, Construct selectedConstruct)
            {
                int renderLineCount = 0;
                foreach (var construct in constructs)
                {
                    if (construct.referenceBlock.Closed ||
                            selectedConstruct != null && 
                            selectedConstruct != construct )
                        continue;
                    if (renderLineCount >= _windowSize + _linesToSkip) return;
 
                    renderLineCount = RenderLine(renderLineCount, () => 
                    {
                        _position.X = _viewport.Width / 32f + _viewport.Position.X;
                        DrawText(construct.referenceBlock.CubeGrid.DisplayName);
                        _position.Y += _lineHeight;
                    });
                    renderLineCount = RenderConstruct(renderLineCount, construct);
                }
            }

            private int RenderConstruct(int renderLineCount, Construct construct)
            {
                if (!_includeFullyConnected &&
                    construct.Islands.Count == 1 &&
                    construct.Islands[0].Segments.Count == 1)
                {
                    return RenderLine(renderLineCount, () => 
                    {
                        _position.X = _viewport.Width / 16f + _viewport.Position.X;
                        DrawText("Fully connected");
                        _position.Y += _lineHeight;
                    });
                }
                return RenderIslandsOf(renderLineCount, construct);
            }

            private int RenderIslandsOf(int renderLineCount, Construct construct)
            {
                foreach (Island island in construct.Islands)
                {
                    if (island.referenceBlock.Closed) continue;
                    for (int segmentIndex = 0; segmentIndex < island.Segments.Count; segmentIndex++)
                    {
                        Segment segment = island.Segments[segmentIndex];
                        for (int blockIndex = 0; blockIndex < segment.Blocks.Count; blockIndex++)
                        {
                            IMyTerminalBlock block = segment.Blocks[blockIndex];
                            if (renderLineCount >= _windowSize + _linesToSkip || block.Closed) break; 

                            renderLineCount = RenderLine(renderLineCount, ()=> 
                            {
                                _position.X = _viewport.Width / 16f + _viewport.Position.X;
                                DrawConveyorSize(segmentIndex, island.Segments, blockIndex, segment.Blocks);
                                _position.Y += _lineHeight;
                            });
                        }
                    }
                }
                return renderLineCount;
            }

            private void DebugConveyorSizes()
            {
                for (var upwards = ConveyorPortCapacity.none; upwards <= ConveyorPortCapacity.large; upwards++)
                {
                    for (var downwards = ConveyorPortCapacity.none; downwards <= ConveyorPortCapacity.large; downwards++)
                    {
                        _position.X = _viewport.Width / 16f + _viewport.Position.X;
                        DrawConnectionSprite(upwards, downwards);
                        _frame.Add(new MySprite()
                        {
                            Type = SpriteType.TEXT,
                            Alignment = TextAlignment.LEFT,
                            Data = upwards.ToString() + ' '+ downwards.ToString(), 
                            Position = new Vector2(32f, -5f) * _regularFontSize + _position,
                            Color = _foregroundColor,
                            FontId = "DEBUG",
                            RotationOrScale = _regularFontSize,
                        }); // Text
                        _position.Y += _lineHeight;
                    }
                }
            }

            private void DrawConveyorSize(int segmentIndex, List<Segment> segments, int blockIndex, List<IMyTerminalBlock> blocks)
            {
                ConveyorPortCapacity upwards, downwards;
                if (blockIndex == 0)
                {
                    if (segmentIndex == 0)
                    {
                        upwards = ConveyorPortCapacity.none;
                    }
                    else upwards = ConveyorPortCapacity.small;
                }
                else upwards = ConveyorPortCapacity.large;

                if (blockIndex == blocks.Count - 1)
                {
                    if (segmentIndex == segments.Count - 1)
                    {
                        downwards = ConveyorPortCapacity.none;
                    }
                    else downwards = ConveyorPortCapacity.small;
                }
                else downwards = ConveyorPortCapacity.large;

                DrawConnectionSprite(upwards, downwards);
                _frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = blocks[blockIndex].DisplayNameText,
                    Position = new Vector2(32f, -5f) * _regularFontSize + _position,
                    Color = _foregroundColor,
                    FontId = "DEBUG",
                    RotationOrScale = _regularFontSize,
                }); // Text
            }

            private void DrawText(string text)
            {
                _frame.Add(new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.LEFT,
                    Data = text,
                    Position = new Vector2(32f, -5f) * _regularFontSize + _position,
                    Color = _foregroundColor,
                    FontId = "DEBUG",
                    RotationOrScale = _regularFontSize,
                }); // Text
            }

            private void ToggleSpriteCache()
            {
                _makeSpriteCacheDirty = !_makeSpriteCacheDirty;
                if (_makeSpriteCacheDirty)
                {
                    _frame.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Color = _surface.BackgroundColor,
                        Position = new Vector2(0, 0),
                        Size = new Vector2(0, 0)
                    });
                }
            }
        }
    }
}
