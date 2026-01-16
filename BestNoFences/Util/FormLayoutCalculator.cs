using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Calculate the size of the rectangular grid according to the number of windows and return it.
/// </summary>
public class FormLayoutCalculator
{
    public static List<Rectangle> CalculateLayout(int formCount, Rectangle screenWorkingArea, bool preferMoreRows = false)
    {
        if (formCount <= 0)
            return new List<Rectangle>();

        if (formCount == 1)
        {
            return new List<Rectangle> { screenWorkingArea };
        }

        int cols, rows;
        CalculateOptimalGrid(formCount, screenWorkingArea.Width, screenWorkingArea.Height, preferMoreRows, out cols, out rows);

        int cellWidth = (screenWorkingArea.Width-screenWorkingArea.X) / cols;
        int cellHeight = (screenWorkingArea.Height-screenWorkingArea.Y) / rows;

        int totalWidthLoss = screenWorkingArea.Width - (cellWidth * cols);
        int totalHeightLoss = screenWorkingArea.Height - (cellHeight * rows);

        List<Rectangle> layouts = new List<Rectangle>();

        for (int index = 0; index < formCount; index++)
        {
            int row = index / cols; 
            int col = index % cols; 

            int x = screenWorkingArea.X + col * cellWidth;
            int y = screenWorkingArea.Y + row * cellHeight;

            int width = cellWidth;
            int height = cellHeight;

            if (col == cols - 1)
            {
                width += totalWidthLoss;
            }
            if (row == rows - 1)
            {
                height += totalHeightLoss;
            }

            layouts.Add(new Rectangle(x, y, width, height));
        }

        return layouts;
    }

    private static void CalculateOptimalGrid(int count, double totalWidth, double totalHeight, bool preferMoreRows, out int cols, out int rows)
    {
        cols = count; 
        rows = 1;     

        double screenAspectRatio = totalWidth / totalHeight;
        double bestDiff = double.MaxValue;

        for (int r = 1; r <= count; r++)
        {
            int c = (count + r - 1) / r; 

            double cellAspectRatio = screenAspectRatio * ((double)r / c);

            double targetRatio = screenAspectRatio;
            double diff = Math.Abs(cellAspectRatio - targetRatio);

            if (diff < bestDiff || (Math.Abs(diff - bestDiff) < 0.001 && preferMoreRows == (r > rows)))
            {
                bestDiff = diff;
                rows = r;
                cols = c;
            }
        }

        if (rows * cols < count)
        {
            cols = (count + rows - 1) / rows;
        }
    }

    public static List<Rectangle> CalculateLayoutOnPrimaryScreen(int formCount, Rectangle usableArea, bool preferMoreRows = false)
    {
        Rectangle screenWorkingArea = usableArea;// Screen.PrimaryScreen.WorkingArea;
        screenWorkingArea.Width = screenWorkingArea.Width - DesktopIconManager.GetDesktopIconSize().Width;
        return CalculateLayout(formCount, screenWorkingArea, preferMoreRows);
    }
}