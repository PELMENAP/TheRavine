using NUnit.Framework;
using Unity.Mathematics;
using TheRavine.EntityControl;

public class ViewDragGestureTests
{
    private static readonly float2 Screen = new(1920f, 1080f);
    private const float Threshold = 6f;

    private static float2 Center => Screen * 0.5f;

    [Test]
    public void SquareIsCenteredWithNinetyPercentOfTheShortSide()
    {
        Assert.AreEqual(972f, ViewDragGesture.SquareSide(Screen), 1e-3f);

        float half = 972f * 0.5f;
        Assert.IsTrue(ViewDragGesture.InsideSquare(Center, Screen));
        Assert.IsTrue(ViewDragGesture.InsideSquare(Center + new float2(half, half), Screen));
        Assert.IsFalse(ViewDragGesture.InsideSquare(Center + new float2(half + 1f, 0f), Screen));
        Assert.IsFalse(ViewDragGesture.InsideSquare(Center + new float2(0f, -half - 1f), Screen));
        Assert.IsFalse(ViewDragGesture.InsideSquare(new float2(10f, 10f), Screen));
    }

    [Test]
    public void PressWithoutMovementIsAClick()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(Center, Screen, false);
        Assert.IsFalse(gesture.Move(Center + new float2(Threshold, 0f), Threshold, out _));
        Assert.IsFalse(gesture.IsDragging);
        Assert.IsTrue(gesture.End(false));
        Assert.IsFalse(gesture.IsTracking);
    }

    [Test]
    public void MovementBeyondTheThresholdIsADragAndSuppressesTheClick()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(Center, Screen, false);

        float2 moved = Center + new float2(Threshold + 1f, 0f);
        Assert.IsTrue(gesture.Move(moved, Threshold, out float2 delta));
        Assert.AreEqual(Threshold + 1f, delta.x, 1e-4f);
        Assert.IsTrue(gesture.IsDragging);

        Assert.IsTrue(gesture.Move(moved + new float2(0f, 4f), Threshold, out delta));
        Assert.AreEqual(4f, delta.y, 1e-4f);

        Assert.IsFalse(gesture.End(false));
    }

    [Test]
    public void ReturningToTheStartAfterADragIsStillADrag()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(Center, Screen, false);
        gesture.Move(Center + new float2(50f, 0f), Threshold, out _);
        gesture.Move(Center, Threshold, out _);
        Assert.IsFalse(gesture.End(false));
    }

    [Test]
    public void DragContinuesOutsideTheSquare()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(Center, Screen, false);
        Assert.IsTrue(gesture.Move(new float2(5f, 5f), Threshold, out float2 delta));
        Assert.Greater(math.length(delta), 0f);
        Assert.IsTrue(gesture.Move(new float2(-200f, 3000f), Threshold, out _));
    }

    [Test]
    public void PressOutsideTheSquareOrOverUiNeverRotates()
    {
        var gesture = new ViewDragGesture();

        gesture.Begin(new float2(10f, 10f), Screen, false);
        Assert.IsFalse(gesture.Move(new float2(200f, 200f), Threshold, out _));
        Assert.IsFalse(gesture.IsDragging);
        Assert.IsFalse(gesture.End(false));

        gesture.Begin(Center, Screen, true);
        Assert.IsFalse(gesture.Move(Center + new float2(100f, 0f), Threshold, out _));
        Assert.IsFalse(gesture.End(false));
    }

    [Test]
    public void ReleaseWithoutMovementOutsideTheSquareIsStillAClick()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(new float2(10f, 10f), Screen, false);
        Assert.IsTrue(gesture.End(false));
    }

    [Test]
    public void CanceledPressIsNotAClick()
    {
        var gesture = new ViewDragGesture();
        gesture.Begin(Center, Screen, false);
        Assert.IsFalse(gesture.End(true));
    }

    [Test]
    public void SensitivityIsNormalisedToTheSideOfTheSquare()
    {
        float yaw = 0f;
        float pitch = 40f;
        ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(-972f, 0f), 972f, 180f, 60f, 15f, 65f);
        Assert.AreEqual(180f, yaw, 1e-3f);
        Assert.AreEqual(40f, pitch, 1e-3f);

        yaw = 0f;
        ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(486f, 0f), 972f, 180f, 60f, 15f, 65f);
        Assert.AreEqual(270f, yaw, 1e-3f);

        float smallSide = 540f * 0.9f;
        yaw = 0f;
        ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(-smallSide, 0f), smallSide, 180f, 60f, 15f, 65f);
        Assert.AreEqual(180f, yaw, 1e-3f);
    }

    [Test]
    public void PitchIsClampedAndYawIsUnbounded()
    {
        float yaw = 350f;
        float pitch = 40f;
        ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(0f, 10000f), 972f, 180f, 60f, 15f, 65f);
        Assert.AreEqual(65f, pitch, 1e-3f);
        ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(0f, -10000f), 972f, 180f, 60f, 15f, 65f);
        Assert.AreEqual(15f, pitch, 1e-3f);

        for (int i = 0; i < 20; i++)
            ViewDragGesture.Rotate(ref yaw, ref pitch, new float2(-972f * 0.1f, 0f), 972f, 180f, 60f, 15f, 65f);
        Assert.GreaterOrEqual(yaw, 0f);
        Assert.Less(yaw, 360f);
    }
}
