# Vector Flow Field Tests

This directory contains tests for the Vector Flow Field (VFF) system using the Unity Test Framework (UTF).

## Test Structure

The tests are organized into the following structure:

- `Tests/` - Root directory for all tests
  - `EditMode/` - Tests that run in Edit mode (no PlayMode required)
  - `PlayMode/` - Tests that require PlayMode to run
  - `TestUtilities.cs` - Common utilities and helpers for tests

## Assembly Definitions

The tests use the following assembly definitions:

- `VFF.Tests.asmdef` - Base assembly for test utilities
- `VFF.Tests.EditMode.asmdef` - Assembly for Edit mode tests
- `VFF.Tests.PlayMode.asmdef` - Assembly for Play mode tests

## Test Categories

Tests are organized into the following categories:

- `Parameters` - Tests for VectorFieldParameters
- `Simulation` - Tests for NavierStokesSolver
- `Integration` - Tests that verify multiple components working together
- `Performance` - Tests that measure performance

## Running Tests

To run the tests in Unity:

1. Open the Test Runner window (Window > General > Test Runner)
2. Select either the "Edit Mode" or "Play Mode" tab
3. Click "Run All" or select specific tests to run

## Writing New Tests

When writing new tests:

1. Place Edit mode tests in the `EditMode/` directory
2. Place Play mode tests in the `PlayMode/` directory
3. Use the `[Test]` attribute for simple tests
4. Use the `[UnityTest]` attribute for tests that need to yield
5. Use the `[Category("CategoryName")]` attribute to categorize tests
6. Use the `[Description("Test description")]` attribute to describe the test
7. Use the `TestUtilities` class for common test operations

Example:

```csharp
[Test]
[Category("Parameters")]
[Description("Verifies that the default grid resolution is valid")]
public void GridResolution_DefaultValue_IsValid()
{
    // Test code here
}
```

## Test Naming Convention

Tests should follow this naming convention:

`MethodName_Condition_ExpectedResult`

For example:
- `GridResolution_DefaultValue_IsValid`
- `Update_WithSink_ChangesVelocityField`
- `SampleField_AtBoundaries_ReturnsValidVectors`

## Test Utilities

The `TestUtilities` class provides helper methods for common test operations:

- `CreateTestTexture` - Creates a test texture with a specified color
- `CreateFieldTexture` - Creates a field texture with sinks and sources
- `LoadComputeShader` - Loads a compute shader or creates a mock
- `CreateParameters` - Creates VectorFieldParameters with specified values
- `SetPrivateField` - Sets a private field using reflection
- `GetPrivateField` - Gets a private field using reflection
- `InvokePrivateMethod` - Invokes a private method using reflection
- `AreApproximatelyEqual` - Asserts that vectors are approximately equal
