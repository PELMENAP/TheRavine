using System.Collections.Generic;
public class DSGroupErrorData
{
    public DSErrorData ErrorData { get; set; }
    public List<DSGroup> Groups { get; set; }

    public DSGroupErrorData()
    {
        ErrorData = new DSErrorData();
        Groups = new List<DSGroup>();
    }
}