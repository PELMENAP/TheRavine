public class DelayedItem
{
    public float[] Input { get; }
    public int Predicted { get; }
    public float Evaluation { get; set; }

    public DelayedItem(float[] input, int pred)
    {
        Input = input;
        Predicted = pred;
        Evaluation = 0.5f;
    }
}