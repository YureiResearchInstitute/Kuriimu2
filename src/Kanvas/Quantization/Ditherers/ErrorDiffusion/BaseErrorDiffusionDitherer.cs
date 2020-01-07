﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Kanvas.Quantization.Helper;
using Kanvas.Quantization.Models.Ditherer;
using Kanvas.Quantization.Models.Parallel;
using Kontract.Kanvas.Quantization;

namespace Kanvas.Quantization.Ditherers.ErrorDiffusion
{
    public abstract class BaseErrorDiffusionDitherer : IColorDitherer
    {
        private int _width;
        private int _height;
        private int _taskCount;
        private IColorCache _colorCache;

        protected abstract byte[,] Matrix { get; }
        protected abstract int MatrixSideWidth { get; }
        protected abstract int MatrixSideHeight { get; }
        protected abstract int ErrorLimit { get; }

        protected float[,] ErrorFactorMatrix { get; private set; }

        public BaseErrorDiffusionDitherer(int width, int height,int taskCount)
        {
            _width = width;
            _height = height;
            _taskCount = taskCount;

            PrepareErrorFactorMatrix();
        }

        private void PrepareErrorFactorMatrix()
        {
            var matrixWidth = Matrix.GetLength(1);
            var matrixHeight = Matrix.GetLength(0);

            ErrorFactorMatrix = new float[matrixHeight, matrixWidth];
            for (int i = 0; i < matrixHeight; i++)
                for (int j = 0; j < matrixWidth; j++)
                    ErrorFactorMatrix[i, j] = Matrix[i, j] / (float)ErrorLimit;
        }

        public IEnumerable<int> Process(IEnumerable<Color> colors, IColorCache colorCache)
        {
            if (ErrorFactorMatrix == null)
                throw new ArgumentNullException(nameof(ErrorFactorMatrix));

            _colorCache = colorCache;

            var colorList = colors.ToList();
            var indices = new int[colorList.Count];
            var errorComponents = new ColorComponentError[colorList.Count];
            for (int i = 0; i < errorComponents.Length; i++)
                errorComponents[i] = new ColorComponentError(0, 0, 0);
            var errors =
                new ErrorDiffusionList<Color, ColorComponentError>(colorList, errorComponents)
                    .ToArray();

            ParallelProcessing.ProcessList(
                errors, indices, _width,
                MatrixSideWidth + 1, _taskCount, ProcessingAction);

            return indices;
        }

        private void ProcessingAction(DelayedLineTask<ErrorDiffusionElement<Color, ColorComponentError>, int[]> delayedLineTask, int index)
        {
            // Get reference elements to work with
            var inputElement = delayedLineTask.Input[index];
            var sourceColor = inputElement.Input;
            var error = inputElement.Error;

            // Add Error component values to source color
            var errorDiffusedColor = Color.FromArgb(
                sourceColor.A,
                GetClampedValue(sourceColor.R + error.RedError, 0, 255),
                GetClampedValue(sourceColor.G + error.GreenError, 0, 255),
                GetClampedValue(sourceColor.B + error.BlueError, 0, 255));

            // Quantize Error diffused source color
            delayedLineTask.Output[index] = _colorCache.GetPaletteIndex(errorDiffusedColor);

            // Retrieve new quantized color for this point
            var targetColor = _colorCache.Palette[delayedLineTask.Output[index]];

            // Calculate errors to distribute for this point
            int redError = errorDiffusedColor.R - targetColor.R;
            int greenError = errorDiffusedColor.G - targetColor.G;
            int blueError = errorDiffusedColor.B - targetColor.B;

            // Retrieve point position
            var pixelX = index % _width;
            var pixelY = index / _width;

            // Process the matrix
            for (int shiftY = -MatrixSideHeight; shiftY <= MatrixSideHeight; shiftY++)
                for (int shiftX = -MatrixSideWidth; shiftX <= MatrixSideWidth; shiftX++)
                {
                    int targetX = pixelX + shiftX;
                    int targetY = pixelY + shiftY;
                    var coefficient = Matrix[shiftY + MatrixSideHeight, shiftX + MatrixSideWidth];
                    var errorFactor = ErrorFactorMatrix[shiftY + MatrixSideHeight, shiftX + MatrixSideWidth];

                    // If substantial Error factor and target point in image bounds
                    if (coefficient != 0 &&
                        targetX >= 0 && targetX < _width &&
                        targetY >= 0 && targetY < _height)
                    {
                        // Add Error to target point for later processing
                        var newTarget = delayedLineTask.Input[targetX + targetY * _width];
                        newTarget.Error.RedError += Convert.ToInt32(errorFactor * redError);
                        newTarget.Error.GreenError += Convert.ToInt32(errorFactor * greenError);
                        newTarget.Error.BlueError += Convert.ToInt32(errorFactor * blueError);
                    }
                }
        }

        private int GetClampedColorElementWithError(int colorElement, float factor, int error)
        {
            int result = Convert.ToInt32(colorElement + factor * error);
            return GetClampedValue(result, 0, 255);
        }

        private int GetClampedValue(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
