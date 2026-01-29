using Fenceless.Model;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Fenceless.Util
{
    
    public class FenceRenderer
    {
        // Cache fonts to reduce memory overhead
        private readonly Font titleFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font iconFont = new Font("Segoe UI", 8);
        private const int shadowDist = 1;
        private readonly FenceInfo _fenceInfo;
        private readonly Logger _logger;

        private bool _disposed = false;

        public FenceRenderer(FenceInfo fenceInfo, Logger logger)
        {
            _fenceInfo = fenceInfo;
            _logger = logger;
        }
     
        public void Render(FencePaintContext ctx, dynamic providers)
        {
            var g = ctx.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            DrawFrame(ctx);
            DrawEntries(ctx, providers);
            DrawScrollbar(ctx);
            if (ctx.IsDragging)
            {
                if (ctx.DragTargetIndex >= 0) RenderDragTargetIndicator(ctx);
                if (ctx.DraggingItemPath != null) RenderDraggedItem(ctx, providers);
            }
        }
        private void DrawFrame(FencePaintContext ctx)
        {
            var g = ctx.Graphics;
            var info = _fenceInfo;

            var bgColor = ApplyTransparency(Color.FromArgb(info.BackgroundColor), info.BackgroundTransparency);
            var titleBgColor = ApplyTransparency(Color.FromArgb(info.TitleBackgroundColor), info.TitleBackgroundTransparency);
            var textColor = ApplyTransparency(Color.FromArgb(info.TextColor), info.TextTransparency);
            var borderColor = ApplyTransparency(Color.FromArgb(info.BorderColor), info.BorderTransparency);

            using (var bgBrush = new SolidBrush(bgColor))
            {
                if (info.CornerRadius > 0)
                {
                    using (var path = CreateRoundedRectanglePath(ctx.ClientRectangle, info.CornerRadius))
                        g.FillPath(bgBrush, path);
                }
                else g.FillRectangle(bgBrush, ctx.ClientRectangle);
            }

            using (var titleBrush = new SolidBrush(titleBgColor))
            {
                var titleRect = new RectangleF(0, 0, ctx.ClientRectangle.Width, ctx.TitleHeight);
                if (info.CornerRadius > 0)
                {
                    using (var path = CreateRoundedRectanglePath(titleRect, info.CornerRadius, true))
                        g.FillPath(titleBrush, path);
                }
                else g.FillRectangle(titleBrush, titleRect);
            }

            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(ctx.WindowText, titleFont, textBrush, new PointF(ctx.ClientRectangle.Width / 2, ctx.TitleOffset),
                    new StringFormat { Alignment = StringAlignment.Center });
            }

            if (info.BorderWidth > 0)
            {
                using (var pen = new Pen(borderColor, info.BorderWidth))
                {
                    if (info.CornerRadius > 0)
                    {
                        var bRect = new Rectangle(info.BorderWidth / 2, info.BorderWidth / 2, ctx.ClientRectangle.Width - info.BorderWidth, ctx.ClientRectangle.Height - info.BorderWidth);
                        using (var path = CreateRoundedRectanglePath(bRect, info.CornerRadius)) g.DrawPath(pen, path);
                    }
                    else g.DrawRectangle(pen, 0, 0, ctx.ClientRectangle.Width - 1, ctx.ClientRectangle.Height - 1);
                }
            }
        }
        private void DrawEntries(FencePaintContext ctx, dynamic providers)
        {
            var g = ctx.Graphics;
            int x = _fenceInfo.ItemSpacing;
            int y = _fenceInfo.ItemSpacing;
            int totalHeight = 0;
            int actualW = Math.Max(_fenceInfo.IconSize + 10, ctx.ItemWidth);
            int actualH = _fenceInfo.IconSize + ctx.TextHeight + 10;
            int visibleHeight = ctx.ClientRectangle.Height - ctx.TitleHeight;
            g.SetClip(new Rectangle(0, ctx.TitleHeight, ctx.ClientRectangle.Width, ctx.ClientRectangle.Height - ctx.TitleHeight));
            if (_fenceInfo.Files.Count == 0)
            {
                ctx.NewScrollHeight = 0;
                return;
            }

            foreach (var file in _fenceInfo.Files)
            {
                var entry = FenceEntry.FromPath(file);
                if (entry == null) continue;

                var rect = new Rectangle(x, y + ctx.TitleHeight - ctx.ScrollOffset, actualW, actualH);
                RenderSingleEntry(ctx, entry, rect, providers);

                if (y + actualH > totalHeight) totalHeight = y + actualH;

                x += actualW + _fenceInfo.ItemSpacing;
                if (x + actualW > ctx.ClientRectangle.Width)
                {
                    x = _fenceInfo.ItemSpacing;
                    y += actualH + _fenceInfo.ItemSpacing;
                }
            }
            ctx.NewScrollHeight = (totalHeight > visibleHeight) ? totalHeight : 0;;
            g.ResetClip();
        }
        // Handle individual item drawing 
        private void RenderSingleEntry(FencePaintContext ctx, FenceEntry entry, Rectangle rect, dynamic providers)
        {
            var g = ctx.Graphics;
            var name = entry.Name;
            var iconSize = _fenceInfo.IconSize;
            var iconBitmap = providers.IconCache.GetIcon(entry.Path, iconSize);
            if (iconBitmap == null) return;

            var format = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            var textMaxSize = new SizeF(ctx.ItemWidth, ctx.TextHeight);
            var textSize = g.MeasureString(name, iconFont, textMaxSize, format);

            int padding = 4; // Edge padding
            int contentHeight = (int)(iconBitmap.Height + 5 + textSize.Height + padding);
            var adaptiveRect = new Rectangle(rect.X, rect.Y-3, rect.Width, contentHeight);
            var outlineRectInner = new Rectangle(adaptiveRect.X + 1, adaptiveRect.Y + 1, adaptiveRect.Width - 2, adaptiveRect.Height - 2);

            bool isBeingDragged = ctx.IsDragging && ctx.DraggingItemPath == entry.Path;
            bool mouseOver = !ctx.IsDragging && rect.Contains(ctx.MousePos);

            if (mouseOver && !isBeingDragged)
            {
                ctx.NewHoveringItem = entry.Path;
                ctx.HasHoverUpdated = true;
                if (ctx.ShouldUpdateSelection) { ctx.NewSelectedItem = entry.Path; ctx.HasSelectionUpdated = true; }
                if (ctx.ShouldRunDoubleClick) entry.Open();
            }

            // Draw the adaptive-height selection/hover background
            if (!isBeingDragged)
            {
                if (ctx.SelectedItem == entry.Path)
                {
                    var color = mouseOver ? Color.FromArgb(120, SystemColors.GradientActiveCaption) : Color.FromArgb(100, SystemColors.GradientInactiveCaption);
                    using (var b = new SolidBrush(color)) g.FillRectangle(b, adaptiveRect);
                    using (var p = new Pen(Color.FromArgb(150, SystemColors.ActiveBorder))) g.DrawRectangle(p, outlineRectInner);
                }
                else if (mouseOver)
                {
                    using (var b = new SolidBrush(Color.FromArgb(50, SystemColors.ActiveCaption))) g.FillRectangle(b, adaptiveRect);
                    using (var p = new Pen(Color.FromArgb(90, SystemColors.ActiveBorder)))
                        g.DrawRectangle(p, adaptiveRect.X, adaptiveRect.Y, adaptiveRect.Width - 1, adaptiveRect.Height - 1);
                }
            }
            float opacity = isBeingDragged ? 0.3f : 1.0f;
            var iconRect = new Rectangle(rect.X + (rect.Width - iconBitmap.Width) / 2, rect.Y, iconBitmap.Width, iconBitmap.Height);

            // draw icon with optional transparency
            if (isBeingDragged)
            {
                using (var attr = new ImageAttributes())
                {
                    var matrix = new ColorMatrix { Matrix33 = opacity };
                    attr.SetColorMatrix(matrix);
                    g.DrawImage(iconBitmap, iconRect, 0, 0, iconBitmap.Width, iconBitmap.Height, GraphicsUnit.Pixel, attr);
                }
            }
            else g.DrawImage(iconBitmap, iconRect);

            // draw text with shadow if enabled
            var textColor = Color.FromArgb(_fenceInfo.TextColor);
            var textRect = new RectangleF(rect.X, rect.Y + iconBitmap.Height + 5, rect.Width, 35);

            if (_fenceInfo.ShowShadow && !isBeingDragged)
            {
                using (var sb = new SolidBrush(Color.FromArgb(180, 15, 15, 15)))
                    g.DrawString(name, iconFont, sb, new RectangleF(textRect.X + shadowDist, textRect.Y + shadowDist, textRect.Width, textRect.Height), format);
            }

            using (var tb = new SolidBrush(Color.FromArgb((int)(255 * opacity), textColor)))
                g.DrawString(name, iconFont, tb, textRect, format);
        }
        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int itemWidth, int itemHeight, int iconSize, Color textColor)
        {
            //try
            //{
            //    var icon = entry.ExtractIcon(thumbnailProvider);
            //    var name = entry.Name;

            //    // Get or create cached scaled bitmap
            //    var cacheKey = $"{entry.Path}_{iconSize}";
            //    var iconBitmap = iconCache.GetIcon(entry.Path, iconSize);

            //    if (iconBitmap == null) return; // Safety check

            //    var textPosition = new PointF(x, y + iconBitmap.Height + 5);
            //    var textMaxSize = new SizeF(itemWidth, textHeight);

            //    var stringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            //    var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
            //    var outlineRect = new Rectangle(x - 2, y - 2, itemWidth + 2, iconBitmap.Height + (int)textSize.Height + 5 + 2);
            //    var outlineRectInner = outlineRect.Shrink(1);

            //    var mousePos = PointToClient(MousePosition);
            //    var mouseOver = !isDraggingItem && mousePos.X >= x && mousePos.Y >= y && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;

            //    // Check if this item is being dragged
            //    var isBeingDragged = isDraggingItem && draggingItem == entry.Path;

            //    if (mouseOver && !isBeingDragged)
            //    {
            //        hoveringItem = entry.Path;
            //        hasHoverUpdated = true;
            //    }

            //    if (mouseOver && shouldUpdateSelection && !isBeingDragged)
            //    {
            //        selectedItem = entry.Path;
            //        shouldUpdateSelection = false;
            //        hasSelectionUpdated = true;
            //    }

            //    if (mouseOver && shouldRunDoubleClick && !isDraggingItem)
            //    {
            //        shouldRunDoubleClick = false;
            //        entry.Open();
            //    }

            //    // Apply transparency and visual effects for dragged items
            //    float opacity = isBeingDragged ? 0.3f : 1.0f;

            //    // Selection and hover highlighting
            //    if (selectedItem == entry.Path && !isBeingDragged)
            //    {
            //        if (mouseOver)
            //        {
            //            g.DrawRectangle(new Pen(Color.FromArgb(180, SystemColors.ActiveBorder), 2), outlineRectInner);
            //            g.FillRectangle(new SolidBrush(Color.FromArgb(120, SystemColors.GradientActiveCaption)), outlineRect);
            //        }
            //        else
            //        {
            //            g.DrawRectangle(new Pen(Color.FromArgb(150, SystemColors.ActiveBorder), 2), outlineRectInner);
            //            g.FillRectangle(new SolidBrush(Color.FromArgb(100, SystemColors.GradientInactiveCaption)), outlineRect);
            //        }
            //    }
            //    else if (!isBeingDragged)
            //    {
            //        if (mouseOver)
            //        {
            //            g.DrawRectangle(new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)), outlineRectInner);
            //            g.FillRectangle(new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption)), outlineRect);
            //        }
            //    }

            //    // Draw icon centered with optional transparency
            //    var iconRect = new Rectangle(x + itemWidth / 2 - iconBitmap.Width / 2, y, iconBitmap.Width, iconBitmap.Height);

            //    if (isBeingDragged)
            //    {
            //        // Use simple alpha blending for dragged items
            //        using (var imageAttributes = new System.Drawing.Imaging.ImageAttributes())
            //        {
            //            var colorMatrix = new System.Drawing.Imaging.ColorMatrix();
            //            colorMatrix.Matrix33 = opacity; // Alpha channel
            //            imageAttributes.SetColorMatrix(colorMatrix);
            //            g.DrawImage(iconBitmap, iconRect, 0, 0, iconBitmap.Width, iconBitmap.Height, GraphicsUnit.Pixel, imageAttributes);
            //        }
            //    }
            //    else
            //    {
            //        g.DrawImage(iconBitmap, iconRect);
            //    }

            //    // Draw text with shadow if enabled
            //    var textColorWithOpacity = isBeingDragged ?
            //        Color.FromArgb((int)(textColor.A * opacity), textColor.R, textColor.G, textColor.B) : textColor;

            //    if (_fenceInfo.ShowShadow && !isBeingDragged) // Skip shadow for dragged items to improve performance
            //    {
            //        using (var shadowBrush = new SolidBrush(Color.FromArgb(180, 15, 15, 15)))
            //        {
            //            g.DrawString(name, iconFont, shadowBrush,
            //                new RectangleF(textPosition.Move(shadowDist, shadowDist), textMaxSize), stringFormat);
            //        }
            //    }

            //    // Draw main text
            //    using (var textBrush = new SolidBrush(textColorWithOpacity))
            //    {
            //        g.DrawString(name, iconFont, textBrush, new RectangleF(textPosition, textMaxSize), stringFormat);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    logger.Error($"Error rendering entry '{entry?.Path}': {ex.Message}", "FenceWindow", ex);

            //    // Draw error placeholder
            //    using (var errorBrush = new SolidBrush(Color.Red))
            //    {
            //        g.FillRectangle(errorBrush, x, y, itemWidth, itemHeight);
            //    }
            //}
        }

        private void DrawScrollbar(FencePaintContext ctx)
        {
            int visibleHeight = ctx.ClientRectangle.Height - ctx.TitleHeight;
            if (ctx.NewScrollHeight <= visibleHeight) return;

            int scrollbarHeight = Math.Max(10, visibleHeight * visibleHeight / ctx.NewScrollHeight);
            var borderColor = Color.FromArgb(_fenceInfo.BorderColor);
            using (var brush = new SolidBrush(Color.FromArgb(150, borderColor)))
            {
                ctx.Graphics.FillRectangle(brush, new Rectangle(ctx.ClientRectangle.Width - 5, ctx.TitleHeight + ctx.ScrollOffset, 5, scrollbarHeight));
            }
        }
        #region Drag Feedback Rendering
        private void RenderDragTargetIndicator(FencePaintContext ctx)
        {
            try
            {
                var g = ctx.Graphics;
                var targetIndex = ctx.DragTargetIndex;

                var itemSpacing = _fenceInfo.ItemSpacing;
                var iconSize = _fenceInfo.IconSize;
                var actualItemWidth = Math.Max(iconSize + 10, ctx.ItemWidth);
                var actualItemHeight = iconSize + ctx.TextHeight + 10;
                var itemsPerRow = Math.Max(1, (_fenceInfo.Width - itemSpacing) / (actualItemWidth + itemSpacing));

                var row = targetIndex / itemsPerRow;
                var col = targetIndex % itemsPerRow;

                var x = itemSpacing + col * (actualItemWidth + itemSpacing);
                var y = itemSpacing + row * (actualItemHeight + itemSpacing) + ctx.TitleHeight - ctx.ScrollOffset;

                // Simple pulsing effect without complex math
                var pulsePhase = (Environment.TickCount / 300) % 4;
                var alpha = pulsePhase < 2 ? 120 : 80;

                using (var pen = new Pen(Color.FromArgb(alpha, SystemColors.Highlight), 2))
                using (var brush = new SolidBrush(Color.FromArgb(alpha / 8, SystemColors.Highlight)))
                {
                    var indicatorRect = new Rectangle(x - 1, y - 1, actualItemWidth + 2, actualItemHeight + 2);

                    // Fill with subtle background
                    g.FillRectangle(brush, indicatorRect);

                    // Draw simple border
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(pen, indicatorRect);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error rendering drag target indicator: {ex.Message}", "FenceWindow", ex);
            }
        }
        private void RenderDraggedItem(FencePaintContext ctx, dynamic providers)
        {
            try
            {
                var g = ctx.Graphics;
                var itemPath = ctx.DraggingItemPath;
                var cursorPosition = ctx.DragCurrentPoint;

                var entry = FenceEntry.FromPath(itemPath);
                if (entry == null) return;

                var iconSize = _fenceInfo.IconSize;
                var cacheKey = $"{entry.Path}_{iconSize}";

                // Use cached icon if available
                var iconBitmap = providers.IconCache.GetIcon(entry.Path, iconSize);

                // If the cache fails, attempt to extract a thumbnail on the fly as a fallback
                if (iconBitmap == null)
                {
                    var icon = entry.ExtractIcon(providers.ThumbnailProvider);
                    if (icon.Width != iconSize || icon.Height != iconSize)
                    {
                        iconBitmap = new Bitmap(iconSize, iconSize);
                        using (var graphics = Graphics.FromImage(iconBitmap))
                        {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.DrawIcon(icon, new Rectangle(0, 0, iconSize, iconSize));
                        }
                    }
                    else
                    {
                        iconBitmap = icon.ToBitmap();
                    }
                }

                if (iconBitmap == null) return;

                // Position the dragged item slightly offset from cursor
                var drawX = cursorPosition.X - iconSize / 2;
                var drawY = cursorPosition.Y - iconSize / 2;

                // Simple shadow without complex effects
                using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.FillEllipse(shadowBrush, drawX + 2, drawY + 2, iconSize, iconSize);
                }

                // Draw the dragged icon with transparency
                using (var imageAttributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    var colorMatrix = new System.Drawing.Imaging.ColorMatrix();
                    colorMatrix.Matrix33 = 0.8f; // Slightly transparent
                    imageAttributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(iconBitmap, new Rectangle(drawX, drawY, iconSize, iconSize),
                        0, 0, iconBitmap.Width, iconBitmap.Height, GraphicsUnit.Pixel, imageAttributes);
                }

                // Draw simplified item name
                var textColor = ApplyTransparency(Color.FromArgb(_fenceInfo.TextColor), _fenceInfo.TextTransparency);
                using (var textBrush = new SolidBrush(Color.FromArgb(180, textColor.R, textColor.G, textColor.B)))
                {
                    var textRect = new RectangleF(drawX - 20, drawY + iconSize + 2, iconSize + 40, 20);
                    var stringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(entry.Name, iconFont, textBrush, textRect, stringFormat);
                }

                // The icon cache manages disposal, so no need to dispose here
                if (iconBitmap == null)
                {
                    _logger.Warning($"Failed to get icon for dragged item '{itemPath}'", "FenceWindow");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error rendering dragged item : {ex.Message}", "FenceWindow", ex);
            }
        }
        #endregion
        private Color ApplyTransparency(Color color, int transparency) => Color.FromArgb((int)(255 * (transparency / 100.0)), color);

        public GraphicsPath CreateRoundedRectanglePath(RectangleF rect, int radius, bool topOnly = false)
        {
            GraphicsPath path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            var diameter = radius * 2;
            var arc = new RectangleF(0, 0, diameter, diameter);

            // Top left corner
            arc.Location = new PointF(rect.Left, rect.Top);
            path.AddArc(arc, 180, 90);

            // Top right corner
            arc.Location = new PointF(rect.Right - diameter, rect.Top);
            path.AddArc(arc, 270, 90);

            if (topOnly)
            {
                // Straight lines for bottom
                path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom);
                path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
                path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Top + radius);
            }
            else
            {
                // Bottom right corner
                arc.Location = new PointF(rect.Right - diameter, rect.Bottom - diameter);
                path.AddArc(arc, 0, 90);

                // Bottom left corner
                arc.Location = new PointF(rect.Left, rect.Bottom - diameter);
                path.AddArc(arc, 90, 90);
            }

            path.CloseFigure();
            return path;
        }





        public void Dispose()
        {
            if (!_disposed)
            {
                titleFont?.Dispose();
                iconFont?.Dispose();
                _disposed = true;
            }
        }
    }
}