using System;
using System.Collections.Generic;
using System.Drawing;

namespace Fenceless.Util
{
    /// <summary>
    /// screen region generator
    /// </summary>
    internal class ScreenRegionGenerator
    {
        public enum RegionType
        {
            Auto,           
            Left,           
            Center,         
            Right,          
            TopRight,       
            TopLeft,        
            BottomRight,    
            BottomLeft,     
            Top,            
            Bottom,         
            LeftSidebar,    
            RightSidebar,   
            Quadrant1,      
            Quadrant2,      
            Quadrant3,      
            Quadrant4       
        }

        public enum ScreenType
        {
            Standard,      
            Wide,          
            UltraWide,     
            Square,        
            Portrait,      
            LowResolution, 
            HighResolution 
        }

        public Dictionary<RegionType, Rectangle> GenerateAllRegions(Size currentResolution)
        {
            var regions = new Dictionary<RegionType, Rectangle>();
            var screenType = DetectScreenType(currentResolution);

            foreach (RegionType regionType in Enum.GetValues(typeof(RegionType)))
            {
                if (regionType != RegionType.Auto) 
                {
                    regions[regionType] = GenerateRegion(currentResolution, regionType, screenType);
                }
            }

            regions[RegionType.Auto] = new Rectangle(10, 10, currentResolution.Width-20, currentResolution.Height-20);

            return regions;
        }

       
        public Rectangle GenerateRegion(Size currentResolution, RegionType regionType)
        {
            var screenType = DetectScreenType(currentResolution);
            return GenerateRegion(currentResolution, regionType, screenType);
        }
            
        private ScreenType DetectScreenType(Size resolution)
        {
            float aspectRatio = (float)resolution.Width / resolution.Height;
            int totalPixels = resolution.Width * resolution.Height;

            if (totalPixels <= 1366 * 768)
                return ScreenType.LowResolution;
            else if (totalPixels > 3840 * 2160)
                return ScreenType.HighResolution;

            if (aspectRatio < 0.8f)
                return ScreenType.Portrait;

            if (aspectRatio > 2.0f)
                return ScreenType.UltraWide;
            else if (aspectRatio > 1.6f)
                return ScreenType.Wide;

            if (aspectRatio > 0.9f && aspectRatio < 1.1f)
                return ScreenType.Square;

            return ScreenType.Standard;
        }
              
        private Rectangle GenerateRegion(Size resolution, RegionType regionType, ScreenType screenType)
        {
            int width = resolution.Width;
            int height = resolution.Height;
            int margin = CalculateMargin(resolution, screenType);

            var layoutParams = GetLayoutParameters(screenType, width, height);

            switch (regionType)
            {
                case RegionType.Left:
                    return GenerateLeftRegion(width, height, margin, layoutParams);

                case RegionType.Center:
                    return GenerateCenterRegion(width, height, margin, layoutParams);

                case RegionType.Right:
                    return GenerateRightRegion(width, height, margin, layoutParams);

                case RegionType.TopRight:
                    return GenerateTopRightRegion(width, height, margin, layoutParams);

                case RegionType.TopLeft:
                    return GenerateTopLeftRegion(width, height, margin, layoutParams);

                case RegionType.BottomRight:
                    return GenerateBottomRightRegion(width, height, margin, layoutParams);

                case RegionType.BottomLeft:
                    return GenerateBottomLeftRegion(width, height, margin, layoutParams);

                case RegionType.Top:
                    return GenerateTopRegion(width, height, margin, layoutParams);

                case RegionType.Bottom:
                    return GenerateBottomRegion(width, height, margin, layoutParams);

                case RegionType.LeftSidebar:
                    return GenerateLeftSidebarRegion(width, height, margin, layoutParams);

                case RegionType.RightSidebar:
                    return GenerateRightSidebarRegion(width, height, margin, layoutParams);

                case RegionType.Quadrant1:
                    return GenerateQuadrant1Region(width, height, margin, layoutParams);

                case RegionType.Quadrant2:
                    return GenerateQuadrant2Region(width, height, margin, layoutParams);

                case RegionType.Quadrant3:
                    return GenerateQuadrant3Region(width, height, margin, layoutParams);

                case RegionType.Quadrant4:
                    return GenerateQuadrant4Region(width, height, margin, layoutParams);

                default:
                    return new Rectangle(0, 0, width, height);
            }
        }

        private int CalculateMargin(Size resolution, ScreenType screenType)
        {
            int baseMargin = 10; 

            if (screenType == ScreenType.LowResolution)
            {
                return Math.Max(5, resolution.Width / 100); 
            }
            else if (screenType == ScreenType.HighResolution)
            {
                return resolution.Width / 80; 
            }
            else if (screenType == ScreenType.UltraWide)
            {
                return resolution.Width / 120; 
            }

            return Math.Max(baseMargin, resolution.Width / 100);
        }
       
        private LayoutParameters GetLayoutParameters(ScreenType screenType, int width, int height)
        {
            var parameters = new LayoutParameters();

            switch (screenType)
            {
                case ScreenType.LowResolution:
                    parameters.LeftRightWidth = width * 4 / 5;        
                    parameters.CenterWidth = width * 4 / 5;          
                    parameters.TopBottomHeight = height;      
                    parameters.CornerWidth = width * 3 / 5;          
                    parameters.CornerHeight = height * 8 / 9;       
                    break;

                case ScreenType.UltraWide:
                    parameters.LeftRightWidth = width * 2 / 3; 
                    parameters.CenterWidth = width * 3 / 5;    
                    parameters.TopBottomHeight = height ;      
                    parameters.CornerWidth = width * 3 / 5;    
                    parameters.CornerHeight = height * 5 / 8;  
                    break;

                case ScreenType.Wide:
                    parameters.LeftRightWidth = width * 3 / 4;
                    parameters.CenterWidth = width / 2;       
                    parameters.TopBottomHeight = height;      
                    parameters.CornerWidth = width * 3 / 5;   
                    parameters.CornerHeight = height * 5 / 8; 
                    break;

                case ScreenType.Portrait:
                    parameters.LeftRightWidth = width * 2 / 5;
                    parameters.CenterWidth = width * 3 / 5;   
                    parameters.TopBottomHeight = height;      
                    parameters.CornerWidth = width * 3 / 5;   
                    parameters.CornerHeight = height * 5 / 8; 
                    break;

                default: // Standard, HighResolution, Square
                    parameters.LeftRightWidth = width * 2 / 3;
                    parameters.CenterWidth = width / 2;       
                    parameters.TopBottomHeight = height ;     
                    parameters.CornerWidth = width * 3 / 5;   
                    parameters.CornerHeight = height * 8 / 9; 
                    break;
            }

            return parameters;
        }

        #region Generate region methods

        private Rectangle GenerateLeftRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                margin,
                margin,
                p.LeftRightWidth - 2 * margin,
                height - 2 * margin
            );
        }

        private Rectangle GenerateCenterRegion(int width, int height, int margin, LayoutParameters p)
        {
            int centerX = (width - p.CenterWidth) / 2;
            return new Rectangle(
                centerX + margin,
                margin,
                p.CenterWidth - 2 * margin,
                height - 2 * margin
            );
        }

        private Rectangle GenerateRightRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                width - p.LeftRightWidth + margin,
                margin,
                p.LeftRightWidth - 2 * margin,
                height - 2 * margin
            );
        }

        private Rectangle GenerateTopRightRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                width - p.CornerWidth + margin,
                margin,
                p.CornerWidth - 2 * margin,
                p.CornerHeight - 2 * margin
            );
        }

        private Rectangle GenerateTopLeftRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                margin,
                margin,
                p.CornerWidth - 2 * margin,
                p.CornerHeight - 2 * margin
            );
        }

        private Rectangle GenerateBottomRightRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                width - p.CornerWidth + margin,
                height - p.CornerHeight + margin,
                p.CornerWidth - 2 * margin,
                p.CornerHeight - 2 * margin
            );
        }

        private Rectangle GenerateBottomLeftRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                margin,
                height - p.CornerHeight + margin,
                p.CornerWidth - 2 * margin,
                p.CornerHeight - 2 * margin
            );
        }

        private Rectangle GenerateTopRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                margin,
                margin,
                width - 2 * margin,
                p.TopBottomHeight - 2 * margin
            );
        }

        private Rectangle GenerateBottomRegion(int width, int height, int margin, LayoutParameters p)
        {
            return new Rectangle(
                margin,
                height - p.TopBottomHeight + margin,
                width - 2 * margin,
                p.TopBottomHeight - 2 * margin
            );
        }

        private Rectangle GenerateLeftSidebarRegion(int width, int height, int margin, LayoutParameters p)
        {
            int sidebarWidth = Math.Min(p.LeftRightWidth, width / 5);
            return new Rectangle(
                margin,
                margin,
                sidebarWidth - 2 * margin,
                height - 2 * margin
            );
        }

        private Rectangle GenerateRightSidebarRegion(int width, int height, int margin, LayoutParameters p)
        {
            int sidebarWidth = Math.Min(p.LeftRightWidth, width / 5);
            return new Rectangle(
                width - sidebarWidth + margin,
                margin,
                sidebarWidth - 2 * margin,
                height - 2 * margin
            );
        }

        private Rectangle GenerateQuadrant1Region(int width, int height, int margin, LayoutParameters p)
        {
            int quadrantWidth = width / 2;
            int quadrantHeight = height / 2;
            return new Rectangle(
                margin,
                margin,
                quadrantWidth - 2 * margin,
                quadrantHeight - 2 * margin
            );
        }

        private Rectangle GenerateQuadrant2Region(int width, int height, int margin, LayoutParameters p)
        {
            int quadrantWidth = width / 2;
            int quadrantHeight = height / 2;
            return new Rectangle(
                quadrantWidth + margin,
                margin,
                quadrantWidth - 2 * margin,
                quadrantHeight - 2 * margin
            );
        }

        private Rectangle GenerateQuadrant3Region(int width, int height, int margin, LayoutParameters p)
        {
            int quadrantWidth = width / 2;
            int quadrantHeight = height / 2;
            return new Rectangle(
                margin,
                quadrantHeight + margin,
                quadrantWidth - 2 * margin,
                quadrantHeight - 2 * margin
            );
        }

        private Rectangle GenerateQuadrant4Region(int width, int height, int margin, LayoutParameters p)
        {
            int quadrantWidth = width / 2;
            int quadrantHeight = height / 2;
            return new Rectangle(
                quadrantWidth + margin,
                quadrantHeight + margin,
                quadrantWidth - 2 * margin,
                quadrantHeight - 2 * margin
            );
        }

        #endregion

        private struct LayoutParameters
        {
            public int LeftRightWidth;   
            public int CenterWidth;      
            public int TopBottomHeight;  
            public int CornerWidth;      
            public int CornerHeight;     
        }

        public string GetScreenReport(Size resolution)
        {
            var screenType = DetectScreenType(resolution);
            float aspectRatio = (float)resolution.Width / resolution.Height;

            return $@"screen report：
resolution: {resolution.Width} × {resolution.Height}
aspectRatio: {aspectRatio:F2}:1
screenType: {screenType}
pixes: {resolution.Width * resolution.Height:N0}
GetRecommendedLayout: {GetRecommendedLayout(screenType)}";
        }

        private string GetRecommendedLayout(ScreenType screenType)
        {
            switch (screenType)
            {
                case ScreenType.LowResolution:
                    return "It is recommended to use a compact layout to avoid too many split screens.";
                case ScreenType.UltraWide:
                    return "It is suitable for left - right split - screen or three - screen layout.";
                case ScreenType.Wide:
                    return "It is suitable for left - right split - screen layout.";
                case ScreenType.Portrait:
                    return "It is suitable for up - down split - screen layout.";
                case ScreenType.HighResolution:
                    return "It is suitable for multi - area split - screen layout.";
                default:
                    return "Standard layout, suitable for various split - screen methods.";
            }
        }
    }
}
