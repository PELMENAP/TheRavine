using System.Diagnostics;
using Priority_Queue;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AStar.Algolgorithms
{
	public class AStarCostMap
	{
		private static float calcHeuristicManhattan(int nodeX, int nodeY, int goalX, int goalY)
		{
			return Mathf.Abs(nodeX - goalX) + Mathf.Abs(nodeY - goalY);
		}

		// Calculates the Euclidean heuristic distance between two nodes
		private static float calcHeuristicEuclidean(int nodeX, int nodeY, int goalX, int goalY)
		{
			return Mathf.Sqrt(Mathf.Pow(nodeX - goalX, 2) + Mathf.Pow(nodeY - goalY, 2));
		}

		// Backtraces the path from the goal node to the start node based on the parent map
		private static (int, int)[] backtracePath((int, int)[,] parentMap, int goalX, int goalY)
		{
			List<(int, int)> path = new List<(int, int)>();

			(int, int) current = (goalX, goalY);

			while (current != (-1, -1))
			{
				path.Add(current);
				current = parentMap[current.Item2, current.Item1];
			}

			path.Reverse();
			return path.ToArray();
		}
		private static (float, (int, int))[] getNeighbours(int xCordinate, int yCordinate, float[,] costMap, bool walkableDiagonals = false)
		{
			List<(float, (int, int))> neighbourCells = new List<(float, (int, int))>();

			int heigth = costMap.GetLength(0);
			int width = costMap.GetLength(1);

			int range = 1;
			int yStart = (int)MathF.Max(0, yCordinate - range);
			int yEnd = (int)MathF.Min(heigth - 1, yCordinate + range);

			int xStart = (int)MathF.Max(0, xCordinate - range);
			int xEnd = (int)MathF.Min(width - 1, xCordinate + range);

			for (int y = yStart; y <= yEnd; y++)
			{
				for (int x = xStart; x <= xEnd; x++)
				{
					if (x == xCordinate && y == yCordinate)
					{
						continue;
					}

					if (costMap[y, x] == -1f)
					{
						continue;
					}

					if (!walkableDiagonals && // If we are not allowing diagonal movement
					(x == xStart || x == xEnd) && (y == yStart || y == yEnd) && // If the node is a diagonal node
					costMap[y, xCordinate] == -1f && costMap[yCordinate, x] == -1f) // If the node is not reachable from the current node
					{
						continue;
					}

					neighbourCells.Add((costMap[y, x], (x, y)));
				}
			}

			return neighbourCells.ToArray();
		}
		public static (int, int)[] GeneratePath(int startX, int startY, int goalX, int goalY, float[,] costMap, bool manhattanHeuristic = true, bool walkableDiagonals = false)
		{
			Func<int, int, int, int, float> heuristic = manhattanHeuristic ? calcHeuristicManhattan : calcHeuristicEuclidean;

			// Get the dimensions of the map
			int mapHeight = costMap.GetLength(0);
			int mapWidth = costMap.GetLength(1);

			// Define constants and arrays needed for the algorithm
			float sqrt2 = Mathf.Sqrt(2);
			float[,] gCostMap = new float[mapHeight, mapWidth];
			float[,] fCostMap = new float[mapHeight, mapWidth];
			(int, int)[,] parentMap = new (int, int)[mapHeight, mapWidth];

			// Initialize the open set with the starting node
			SimplePriorityQueue<(int, int)> openSet = new SimplePriorityQueue<(int, int)>();
			gCostMap[startY, startX] = 0.0000001f;
			fCostMap[startY, startX] = heuristic(startX, startY, goalX, goalY);
			parentMap[startY, startX] = (-1, -1);
			openSet.Enqueue((startX, startY), fCostMap[startY, startX]);

			while (openSet.Count > 0) // While there are still nodes in the open set
			{
				// Get the node with the lowest f cost from the open set
				(int, int) current = openSet.First;
				int currentX = current.Item1;
				int currentY = current.Item2;

				// If we have reached the goal node, backtrack to get the path
				if (current.Item1 == goalX && current.Item2 == goalY)
				{
					return backtracePath(parentMap, goalX, goalY);
				}

				openSet.Dequeue();
				(float, (int, int))[] neighbours = getNeighbours(currentX, currentY, costMap, walkableDiagonals);

				// Loop through all the neighbours of the current node
				for (int i = 0; i < neighbours.Length; i++)
				{
					(float, (int, int)) neighbour = neighbours[i];

					int neighbourX = neighbour.Item2.Item1;
					int neighbourY = neighbour.Item2.Item2;

					// Calculate the tentative g-cost of the neighbour using the distance between the current node and the neighbour, and the cost of the neighbour
					float dist = currentX - neighbourX == 0 || currentY - neighbourY == 0 ? 1 : sqrt2;
					float tentativeGCost = gCostMap[currentY, currentX] + dist * neighbour.Item1;

					if (gCostMap[neighbourY, neighbourX] == 0 || tentativeGCost < gCostMap[neighbourY, neighbourX])
					{
						parentMap[neighbourY, neighbourX] = current;
						gCostMap[neighbourY, neighbourX] = tentativeGCost;
						fCostMap[neighbourY, neighbourX] = tentativeGCost + heuristic(neighbourX, neighbourY, goalX, goalY);

						if (!openSet.Contains(neighbour.Item2))
						{
							openSet.Enqueue(neighbour.Item2, fCostMap[neighbourY, neighbourX]);
						}
					}
				}
			}

			return new (int, int)[0];
		}
	}
}