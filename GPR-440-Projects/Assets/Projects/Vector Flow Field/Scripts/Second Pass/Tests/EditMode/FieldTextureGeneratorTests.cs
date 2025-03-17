using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFF;

namespace VFF.Tests.EditMode
{
    public class FieldTextureGeneratorTests
    {
        private FieldTextureGenerator fieldGenerator;
        private Vector2Int resolution = new Vector2Int(64, 64);

        [SetUp]
        public void Setup()
        {
            // Create a new instance of FieldTextureGenerator
            fieldGenerator = new FieldTextureGenerator(resolution);
        }

        [TearDown]
        public void Teardown()
        {
            // Clean up
            fieldGenerator = null;
        }

        [Test]
        public void Constructor_WithValidResolution_CreatesFieldTexture()
        {
            // Check that the field texture was created
            Assert.IsNotNull(fieldGenerator.FieldTexture, "Field texture should not be null");
            Assert.AreEqual(resolution.x, fieldGenerator.FieldTexture.width, "Field texture width should match resolution");
            Assert.AreEqual(resolution.y, fieldGenerator.FieldTexture.height, "Field texture height should match resolution");
        }

        [Test]
        public void SetFullField_SetsAllPixelsToWhite()
        {
            // Set the full field
            fieldGenerator.SetFullField();

            // Check that all pixels are white
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color[] pixels = fieldTexture.GetPixels();
            foreach (Color pixel in pixels)
            {
                Assert.AreEqual(Color.white, pixel, "All pixels should be white");
            }
        }

        [Test]
        public void AddSink_AddsRedPixelsAtSpecifiedPosition()
        {
            // Add a sink at the center
            Vector2 position = new Vector2(0.5f, 0.5f);
            float radius = 0.1f;
            fieldGenerator.AddSink(position, radius);

            // Check that pixels at the center are red
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color centerPixel = fieldTexture.GetPixel(
                Mathf.RoundToInt(position.x * resolution.x),
                Mathf.RoundToInt(position.y * resolution.y)
            );
            Assert.AreEqual(Color.red, centerPixel, "Center pixel should be red");
        }

        [Test]
        public void AddSource_AddsGreenPixelsAtSpecifiedPosition()
        {
            // Add a source at the center
            Vector2 position = new Vector2(0.5f, 0.5f);
            float radius = 0.1f;
            fieldGenerator.AddSource(position, radius);

            // Check that pixels at the center are green
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color centerPixel = fieldTexture.GetPixel(
                Mathf.RoundToInt(position.x * resolution.x),
                Mathf.RoundToInt(position.y * resolution.y)
            );
            Assert.AreEqual(Color.green, centerPixel, "Center pixel should be green");
        }

        [Test]
        public void AddObstacle_AddsBlackPixelsAtSpecifiedPosition()
        {
            // Set the full field first
            fieldGenerator.SetFullField();

            // Add an obstacle at the center
            Vector2 position = new Vector2(0.5f, 0.5f);
            float radius = 0.1f;
            fieldGenerator.AddObstacle(position, radius);

            // Check that pixels at the center are black
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color centerPixel = fieldTexture.GetPixel(
                Mathf.RoundToInt(position.x * resolution.x),
                Mathf.RoundToInt(position.y * resolution.y)
            );
            Assert.AreEqual(Color.black, centerPixel, "Center pixel should be black");
        }

        [Test]
        public void SetFieldRect_SetsWhitePixelsInRectangle()
        {
            // Clear the field first
            fieldGenerator.ClearField();

            // Set a rectangular field area
            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 size = new Vector2(0.5f, 0.5f);
            fieldGenerator.SetFieldRect(center, size);

            // Check that pixels in the rectangle are white
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            int minX = Mathf.RoundToInt((center.x - size.x / 2) * resolution.x);
            int maxX = Mathf.RoundToInt((center.x + size.x / 2) * resolution.x);
            int minY = Mathf.RoundToInt((center.y - size.y / 2) * resolution.y);
            int maxY = Mathf.RoundToInt((center.y + size.y / 2) * resolution.y);

            Color centerPixel = fieldTexture.GetPixel(
                Mathf.RoundToInt(center.x * resolution.x),
                Mathf.RoundToInt(center.y * resolution.y)
            );
            Assert.AreEqual(Color.white, centerPixel, "Center pixel should be white");

            // Check a pixel outside the rectangle
            Color outsidePixel = fieldTexture.GetPixel(0, 0);
            Assert.AreEqual(Color.black, outsidePixel, "Outside pixel should be black");
        }

        [Test]
        public void SetFieldCircle_SetsWhitePixelsInCircle()
        {
            // Clear the field first
            fieldGenerator.ClearField();

            // Set a circular field area
            Vector2 center = new Vector2(0.5f, 0.5f);
            float radius = 0.25f;
            fieldGenerator.SetFieldCircle(center, radius);

            // Check that pixels in the circle are white
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color centerPixel = fieldTexture.GetPixel(
                Mathf.RoundToInt(center.x * resolution.x),
                Mathf.RoundToInt(center.y * resolution.y)
            );
            Assert.AreEqual(Color.white, centerPixel, "Center pixel should be white");

            // Check a pixel outside the circle
            Color outsidePixel = fieldTexture.GetPixel(0, 0);
            Assert.AreEqual(Color.black, outsidePixel, "Outside pixel should be black");
        }

        [Test]
        public void ClearSinksAndSources_RemovesRedAndGreenPixels()
        {
            // Set the full field
            fieldGenerator.SetFullField();

            // Add a sink and a source
            fieldGenerator.AddSink(new Vector2(0.25f, 0.5f), 0.1f);
            fieldGenerator.AddSource(new Vector2(0.75f, 0.5f), 0.1f);

            // Clear sinks and sources
            fieldGenerator.ClearSinksAndSources();

            // Check that all pixels are either white or black
            Texture2D fieldTexture = fieldGenerator.FieldTexture;
            Color[] pixels = fieldTexture.GetPixels();
            foreach (Color pixel in pixels)
            {
                Assert.IsTrue(
                    pixel == Color.white || pixel == Color.black,
                    "All pixels should be white or black"
                );
            }
        }
    }
}
