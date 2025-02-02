using UnityEngine;
using UnityEngine.Tilemaps;

public class addtile : MonoBehaviour
{
    public Tilemap tilemap;
    public Tilemap emptyTilemap;
    public Tile tile;

    private void Start() 
    {
        tilemap = new Tilemap();
        tilemap.SetTile(new Vector3Int(0,0,0), null);

        for(int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                tilemap.SetTile(new Vector3Int(i, j, 0), null);
            }
        }
    }
}
