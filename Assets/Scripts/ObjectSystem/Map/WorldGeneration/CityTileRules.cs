
public static class CityTileRules
{
    public static bool GrassRule0(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2);
    }

    public static bool GrassRule1(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4);
    }

    public static bool GrassRule2(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2);
    }
    public static bool GrassRule3(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }
    public static bool GrassRule4(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2);
    }
    public static bool GrassRule5(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4);
    }
    public static bool GrassRule6(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4);
    }
    public static bool GrassRule7(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }
    public static bool GrassRule8(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }
    public static bool GrassRule9(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2);
    }
    public static bool GrassRule10(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }

    public static bool GrassRule11(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 0 || map[x, y + 1] == 2) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }

    public static bool GrassRule12(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 0 || map[x, y - 1] == 2);
    }
    public static bool GrassRule13(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 0 || map[x - 1, y] == 2) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }
    public static bool GrassRule14(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 0 || map[x + 1, y] == 2) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }

    public static bool GrassRule15(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 1 || map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 3 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 3 || map[x, y - 1] == 4);
    }

    public static bool RoadRule0(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 4);
    }

    public static bool RoadRule1(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 4);
    }

    public static bool RoadRule2(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 4);
    }

    public static bool RoadRule3(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 4);
    }

    public static bool RoadRule4(int[,] map, int x, int y)
    {
        return (map[x, y + 1] == 1 || map[x, y + 1] == 4) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 4);
    }

    public static bool RoadRule5(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4);
    }

    public static bool RoadRule6(int[,] map, int x, int y)
    {
        return (map[x, y - 1] == 1 || map[x, y - 1] == 4) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 4);
    }
    public static bool RoadRule7(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 2 || map[x - 1, y] == 3) &&
        (map[x + 1, y] == 1 || map[x + 1, y] == 4);
    }

    public static bool RoadRule8(int[,] map, int x, int y)
    {
        return (map[x + 1, y] == 2 || map[x + 1, y] == 3) &&
        (map[x - 1, y] == 1 || map[x - 1, y] == 4);
    }

    public static bool RoadRule9(int[,] map, int x, int y)
    {
        return (map[x, y - 1] == 2 || map[x, y - 1] == 3) &&
        (map[x, y + 1] == 1 || map[x, y + 1] == 4);
    }
    public static bool RoadRule10(int[,] map, int x, int y)
    {
        return (map[x, y + 1] == 2 || map[x, y + 1] == 3) &&
        (map[x, y - 1] == 1 || map[x, y - 1] == 4);
    }

    public static bool RiverRule0(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule1(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule2(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule3(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] != 2;
    }
    public static bool RiverRule4(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule5(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule6(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule7(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] != 2;
    }
    public static bool RiverRule8(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] != 2;
    }
    public static bool RiverRule9(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] == 2;
    }

    public static bool RiverRule10(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] == 2 &&
        map[x, y + 1] != 2;
    }

    public static bool RiverRule11(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] != 2 &&
        map[x + 1, y] == 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] != 2;
    }
    public static bool RiverRule12(int[,] map, int x, int y)
    {
        return 
        map[x - 1, y] == 2 &&
        map[x + 1, y] != 2 &&
        map[x, y - 1] != 2 &&
        map[x, y - 1] != 3 &&
        map[x, y - 1] != 4 &&
        map[x, y + 1] != 2;
    }


    public static bool BridgeRule0(int[,] map, int x, int y)
    {
        return 
        (map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 3 || map[x + 1, y] == 4) &&
        (map[x, y - 1] == 3 || map[x, y - 1] == 4) &&
        (map[x, y + 1] == 3 || map[x, y + 1] == 4);
    }

    public static bool BridgeRule1(int[,] map, int x, int y)
    {
        return false;
    }
    public static bool BridgeRule2(int[,] map, int x, int y)
    {
        return false;
    }
    public static bool BridgeRule3(int[,] map, int x, int y)
    {
        return false;
    }
    public static bool BridgeRule4(int[,] map, int x, int y)
    {
        return false;
    }
    public static bool BridgeRule5(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 3 || map[x - 1, y] == 4) &&
        (map[x + 1, y] == 3 || map[x + 1, y] == 4);
    }
    public static bool BridgeRule6(int[,] map, int x, int y)
    {
        return (map[x, y - 1] == 3 || map[x, y - 1] == 4) &&
        (map[x, y + 1] == 3 || map[x, y + 1] == 4);
    }

    public static bool AwayBridgeRule0(int[,] map, int x, int y)
    {
        return (map[x - 1, y] == 1 || map[x - 1, y] == 4) && map[x + 1, y] == 3;
    }

    public static bool AwayBridgeRule1(int[,] map, int x, int y)
    {
        return map[x - 1, y] == 3 && (map[x + 1, y] == 1 || map[x + 1, y] == 4);
    }

    public static bool AwayBridgeRule2(int[,] map, int x, int y)
    {
        return map[x, y - 1] == 3 && (map[x, y + 1] == 1 || map[x, y + 1] == 4);
    }

    public static bool AwayBridgeRule3(int[,] map, int x, int y)
    {
        return (map[x, y - 1] == 1 || map[x, y - 1] == 4) && map[x, y + 1] == 3;
    }

    public static bool SquareRule0(int[,] map, int x, int y)
    {
        return true;
    }
}