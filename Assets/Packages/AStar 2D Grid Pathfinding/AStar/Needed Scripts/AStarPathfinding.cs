using System.Threading.Tasks;
using AStar.Algolgorithms;

namespace AStar
{
	public class AStarPathfinding
	{
		/// <summary>
		/// Asynchronously generates a path from (startX, startY) to (goalX, goalY) on the 2D walkableMap bool array.
		/// Start and Goal represent cordinates in the walkableMap, 
		/// the walkableMap is ordered [rows, collumns] aka [y, x] and is used to determen if we are able to walk on certain tiles (true) or not (false).
		/// </summary>
		/// <param name="startX">The X cordinate for the Start of our path</param>
		/// <param name="startY">The Y cordinate for the Start of our path</param>
		/// <param name="goalX">The X cordinate for the Goal of our path</param>
		/// <param name="goalY">The Y cordinate for the Goal of our path</param>
		/// <param name="walkableMap">A 2D array indicating whether a tile is traverable or not. 
		/// The walkableMap is ordered [rows, collumns] i.e [y, x]</param>
		/// <param name="manhattanHeuristic">If true the heuristic uses manhattan distance, if false it uses euclidean distance</param>
		/// <param name="walkableDiagonals">If true diagonal movement is allowed even if it is not reachable via a horizontal and a vertical move</param>
		/// <returns>Asynchronously returns a (int, int) array representing path cordinates traveling from start to goal. The codniates are ordered (x, y). 
		/// Returns a empty (int, int) array if no path is found. I.e (int, int)[]</returns>
		public static async Task<(int, int)[]> GeneratePath(int startX, int startY, int goalX, int goalY, bool[,] walkableMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			return await Task.Run(() => AStarBoolMap.GeneratePath(startX, startY, goalX, goalY, walkableMap, manhattanHeuristic, walkableDiagonals));
		}

		/// <summary>
		/// Synchronously generates a path from (startX, startY) to (goalX, goalY) on the 2D walkableMap bool array.
		/// Start and Goal represent cordinates in the walkableMap, 
		/// the walkableMap is ordered [rows, collumns] aka [y, x] and is used to determen if we are able to walk on certain tiles (true) or not (false).
		/// WARNING: This function will by default be running on unitys thread and this might cuase momentary freezing if a hard path is calculated. 
		/// If this occurs use the async variant: GeneratePath
		/// </summary>
		/// <param name="startX">The X cordinate for the Start of our path</param>
		/// <param name="startY">The Y cordinate for the Start of our path</param>
		/// <param name="goalX">The X cordinate for the Goal of our path</param>
		/// <param name="goalY">The Y cordinate for the Goal of our path</param>
		/// <param name="walkableMap">A 2D array indicating whether a tile is traverable or not. 
		/// The walkableMap is ordered [rows, collumns] i.e [y, x]</param>
		/// <param name="manhattanHeuristic">If true the heuristic uses manhattan distance, if false it uses euclidean distance</param>
		/// <param name="walkableDiagonals">If true diagonal movement is allowed even if it is not reachable via a horizontal and a vertical move</param>
		/// <returns>Returns a (int, int) array representing path cordinates traveling from start to goal. The codniates are ordered (x, y). 
		/// Returns a empty (int, int) array if no path is found. I.e (int, int)[]</returns>
		public static (int, int)[] GeneratePathSync(int startX, int startY, int goalX, int goalY, bool[,] walkableMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			return AStarBoolMap.GeneratePath(startX, startY, goalX, goalY, walkableMap, manhattanHeuristic, walkableDiagonals);
		}

		/// <summary>
		/// Asynchronously generates a path from (startX, startY) to (goalX, goalY) on the 2D costMap float array.
		/// Start and Goal represent cordinates in the costMap, 
		/// the costMap is ordered [rows, collumns] aka [y, x] and is used to determen the cost of traveling through tiles, 
		/// lower cost tiles are easier to move through than higher cost tiles (-1f if the tile is not walkable).
		/// </summary>
		/// <param name="startX">The X cordinate for the Start of our path</param>
		/// <param name="startY">The Y cordinate for the Start of our path</param>
		/// <param name="goalX">The X cordinate for the Goal of our path</param>
		/// <param name="goalY">The Y cordinate for the Goal of our path</param>
		/// <param name="costMap">A 2D array indicating the cost of traveling through tiles (-1f if the tile is not walkable). 
		/// The costMap is ordered [rows, collumns] i.e [y, x]</param>
		/// <param name="manhattanHeuristic">If true the heuristic uses manhattan distance, if false it uses euclidean distance</param>
		/// <param name="walkableDiagonals">If true diagonal movement is allowed even if it is not reachable via a horizontal and a vertical move</param>
		/// <returns>Returns a (int, int) array representing path cordinates traveling from start to goal. The codniates are ordered (x, y). 
		/// Asynchronously returns a empty (int, int) array if no path is found. I.e (int, int)[]</returns>
		public static async Task<(int, int)[]> GeneratePath(int startX, int startY, int goalX, int goalY, float[,] costMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			return await Task.Run(() => AStarCostMap.GeneratePath(startX, startY, goalX, goalY, costMap, manhattanHeuristic, walkableDiagonals));
		}

		/// <summary>
		/// Synchronously generates a path from (startX, startY) to (goalX, goalY) on the 2D costMap float array.
		/// Start and Goal represent cordinates in the costMap, 
		/// the costMap is ordered [rows, collumns] aka [y, x] and is used to determen the cost of traveling through tiles, 
		/// lower cost tiles are easier to move through than higher cost tiles (-1f if the tile is not walkable). 
		/// WARNING: This function will by default be running on unitys thread and this might cuase momentary freezing if a hard path is calculated. 
		/// If this occurs use the async variant: GeneratePath
		/// </summary>
		/// <param name="startX">The X cordinate for the Start of our path</param>
		/// <param name="startY">The Y cordinate for the Start of our path</param>
		/// <param name="goalX">The X cordinate for the Goal of our path</param>
		/// <param name="goalY">The Y cordinate for the Goal of our path</param>
		/// <param name="costMap">A 2D array indicating the cost of traveling through tiles (-1f if the tile is not walkable). 
		/// The costMap is ordered [rows, collumns] i.e [y, x]</param>
		/// <param name="manhattanHeuristic">If true the heuristic uses manhattan distance, if false it uses euclidean distance</param>
		/// <param name="walkableDiagonals">If true diagonal movement is allowed even if it is not reachable via a horizontal and a vertical move</param>
		/// <returns>Returns a (int, int) array representing path cordinates traveling from start to goal. The codniates are ordered (x, y). 
		/// Returns a empty (int, int) array if no path is found. I.e (int, int)[]</returns>
		public static (int, int)[] GeneratePathSync(int startX, int startY, int goalX, int goalY, float[,] costMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			return AStarCostMap.GeneratePath(startX, startY, goalX, goalY, costMap, manhattanHeuristic, walkableDiagonals);
		}
	}
}