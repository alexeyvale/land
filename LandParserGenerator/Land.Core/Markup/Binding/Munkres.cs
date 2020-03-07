/*
    The MIT License (MIT)

    Copyright (c) 2000 Robert A. Pilgrim
                       Murray State University
                       Dept. of Computer Science & Information Systems
                       Murray,Kentucky

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
   SOFTWARE.

 */

using System;
using System.Linq;

namespace Land.Markup.Binding
{
	public class AssignmentProblem
	{
		public bool Transposed { get; set; } = false;
		public int[,] Weights { get; set; }
		public int[,] M { get; set; }
		public int[,] Path { get; set; }
		public int[] RowCover { get; set; }
		public int[] ColCover { get; set; }
		public int NRow { get; set; }
		public int NCol { get; set; }

		public int PathCount { get; set; } = 0;
		public int PathRow0 { get; set; }
		public int PathCol0 { get; set; }
		public int Asgn { get; set; } = 0;
		public int Step { get; set; }

		//For each row of the cost matrix, find the smallest element and subtract
		//it from every element in its row.  When finished, Go to Step 2.
		private void StepOne()
		{
			int min_in_row;

			for (int r = 0; r < NRow; r++)
			{
				min_in_row = Weights[r, 0];
				for (int c = 0; c < NCol; c++)
					if (Weights[r, c] < min_in_row)
						min_in_row = Weights[r, c];
				for (int c = 0; c < NCol; c++)
					Weights[r, c] -= min_in_row;
			}

			Step = 2;
		}

		//Find a zero (Z) in the resulting matrix.  If there is no starred 
		//zero in its row or column, star Z. Repeat for each element in the 
		//matrix. Go to Step 3.
		private void StepTwo()
		{
			for (int r = 0; r < NRow; r++)
				for (int c = 0; c < NCol; c++)
				{
					if (Weights[r, c] == 0 && RowCover[r] == 0 && ColCover[c] == 0)
					{
						M[r, c] = 1;
						RowCover[r] = 1;
						ColCover[c] = 1;
					}
				}
			for (int r = 0; r < NRow; r++)
				RowCover[r] = 0;
			for (int c = 0; c < NCol; c++)
				ColCover[c] = 0;

			Step = 3;
		}

		//Cover each column containing a starred zero.  If K columns are covered, 
		//the starred zeros describe a complete set of unique assignments.  In this 
		//case, Go to DONE, otherwise, Go to Step 4.
		private void StepThree()
		{
			int colcount;
			for (int r = 0; r < NRow; r++)
				for (int c = 0; c < NCol; c++)
					if (M[r, c] == 1)
						ColCover[c] = 1;

			colcount = 0;
			for (int c = 0; c < NCol; c++)
				if (ColCover[c] == 1)
					colcount += 1;
			if (colcount >= NCol || colcount >= NRow)
				Step = 7;
			else
				Step = 4;
		}

		//methods to support step 4
		private void find_a_zero(ref int row, ref int col)
		{
			int r = 0;
			int c;
			bool done;
			row = -1;
			col = -1;
			done = false;
			while (!done)
			{
				c = 0;
				while (true)
				{
					if (Weights[r, c] == 0 && RowCover[r] == 0 && ColCover[c] == 0)
					{
						row = r;
						col = c;
						done = true;
					}
					c += 1;
					if (c >= NCol || done)
						break;
				}
				r += 1;
				if (r >= NRow)
					done = true;
			}
		}

		private bool star_in_row(int row)
		{
			bool tmp = false;
			for (int c = 0; c < NCol; c++)
				if (M[row, c] == 1)
					tmp = true;
			return tmp;
		}

		private void find_star_in_row(int row, ref int col)
		{
			col = -1;
			for (int c = 0; c < NCol; c++)
				if (M[row, c] == 1)
					col = c;
		}

		//Find a noncovered zero and prime it.  If there is no starred zero 
		//in the row containing this primed zero, Go to Step 5.  Otherwise, 
		//cover this row and uncover the column containing the starred zero. 
		//Continue in this manner until there are no uncovered zeros left. 
		//Save the smallest uncovered value and Go to Step 6.
		private void StepFour()
		{
			int row = -1;
			int col = -1;
			bool done;

			done = false;
			while (!done)
			{
				find_a_zero(ref row, ref col);
				if (row == -1)
				{
					done = true;
					Step = 6;
				}
				else
				{
					M[row, col] = 2;
					if (star_in_row(row))
					{
						find_star_in_row(row, ref col);
						RowCover[row] = 1;
						ColCover[col] = 0;
					}
					else
					{
						done = true;
						Step = 5;
						PathRow0 = row;
						PathCol0 = col;
					}
				}
			}
		}

		// methods to support step 5
		private void find_star_in_col(int c, ref int r)
		{
			r = -1;
			for (int i = 0; i < NRow; i++)
				if (M[i, c] == 1)
					r = i;
		}

		private void find_prime_in_row(int r, ref int c)
		{
			for (int j = 0; j < NCol; j++)
				if (M[r, j] == 2)
					c = j;
		}

		private void augment_path()
		{
			for (int p = 0; p < PathCount; p++)
				if (M[Path[p, 0], Path[p, 1]] == 1)
					M[Path[p, 0], Path[p, 1]] = 0;
				else
					M[Path[p, 0], Path[p, 1]] = 1;
		}

		private void clear_covers()
		{
			for (int r = 0; r < NRow; r++)
				RowCover[r] = 0;
			for (int c = 0; c < NCol; c++)
				ColCover[c] = 0;
		}

		private void erase_primes()
		{
			for (int r = 0; r < NRow; r++)
				for (int c = 0; c < NCol; c++)
					if (M[r, c] == 2)
						M[r, c] = 0;
		}


		//Construct a series of alternating primed and starred zeros as follows.  
		//Let Z0 represent the uncovered primed zero found in Step 4.  Let Z1 denote 
		//the starred zero in the column of Z0 (if any). Let Z2 denote the primed zero 
		//in the row of Z1 (there will always be one).  Continue until the series 
		//terminates at a primed zero that has no starred zero in its column.  
		//Unstar each starred zero of the series, star each primed zero of the series, 
		//erase all primes and uncover every line in the matrix.  Return to Step 3.
		private void StepFive()
		{
			bool done;
			int r = -1;
			int c = -1;

			PathCount = 1;
			Path[PathCount - 1, 0] = PathRow0;
			Path[PathCount - 1, 1] = PathCol0;
			done = false;
			while (!done)
			{
				find_star_in_col(Path[PathCount - 1, 1], ref r);
				if (r > -1)
				{
					PathCount += 1;
					Path[PathCount - 1, 0] = r;
					Path[PathCount - 1, 1] = Path[PathCount - 2, 1];
				}
				else
					done = true;
				if (!done)
				{
					find_prime_in_row(Path[PathCount - 1, 0], ref c);
					PathCount += 1;
					Path[PathCount - 1, 0] = Path[PathCount - 2, 0];
					Path[PathCount - 1, 1] = c;
				}
			}
			augment_path();
			clear_covers();
			erase_primes();
			Step = 3;
		}

		//methods to support step 6
		private void find_smallest(ref int minval)
		{
			for (int r = 0; r < NRow; r++)
				for (int c = 0; c < NCol; c++)
					if (RowCover[r] == 0 && ColCover[c] == 0)
						if (minval > Weights[r, c])
							minval = Weights[r, c];
		}

		//Add the value found in Step 4 to every element of each covered row, and subtract 
		//it from every element of each uncovered column.  Return to Step 4 without 
		//altering any stars, primes, or covered lines.
		private void StepSix()
		{
			int minval = int.MaxValue;
			find_smallest(ref minval);
			for (int r = 0; r < NRow; r++)
				for (int c = 0; c < NCol; c++)
				{
					if (RowCover[r] == 1)
						Weights[r, c] += minval;
					if (ColCover[c] == 0)
						Weights[r, c] -= minval;
				}
			Step = 4;
		}

		private int[,] Transpose(int[,] matrix)
		{
			var newMatrix = new int[
				matrix.GetLength(1), 
				matrix.GetLength(0)
			];

			for (int i = 0; i < matrix.GetLength(0); i++)
			{
				for (int j = 0; j < matrix.GetLength(1); j++)
				{
					newMatrix[j, i] = matrix[i, j];
				}
			}

			return newMatrix;
		}

		public int[] Compute1(int[,] weights)
		{
			if(weights.GetLength(0) > weights.GetLength(1))
			{
				Transposed = true;
				Weights = Transpose(weights);
			}
			else
			{
				Weights = weights;
			}

			NRow = Weights.GetLength(0);
			NCol = Weights.GetLength(1);

			M = new int[NRow, NCol];

			RowCover = new int[NRow];
			ColCover = new int[NCol];
			Path = new int[NRow + NCol, 2];

			Step = 1;
			var done = false;
			while (!done)
			{
				switch (Step)
				{
					case 1:
						StepOne();
						break;
					case 2:
						StepTwo();
						break;
					case 3:
						StepThree();
						break;
					case 4:
						StepFour();
						break;
					case 5:
						StepFive();
						break;
					case 6:
						StepSix();
						break;
					case 7:
						done = true;
						break;
				}
			}

			if (Transposed)
				M = Transpose(M);

			var result = new int[M.GetLength(0)];

			for (var i = 0; i < M.GetLength(0); ++i)
			{
				result[i] = Enumerable.Range(0, M.GetLength(1))
					.Select(j => new { idx = j, elem = M[i, j] })
					.FirstOrDefault(e => e.elem == 1)
					?.idx ?? -1;
			}

			return result;
		}

		public int[] Compute2(int[,] weights)
		{
			if (weights.GetLength(0) > weights.GetLength(1))
				weights = Transpose(weights);

			var lines = new double[weights.GetLength(0)];
			var columns = new double[weights.GetLength(1)];
			var matching = new int[columns.Length];
			var way = new int[columns.Length];

			for (int i = 1; i < lines.Length; ++i)
			{
				matching[0] = i;

				var j0 = 0;
				var minv = new double[columns.Length];
				var used = new bool[columns.Length];

				do
				{
					used[j0] = true;
					int i0 = matching[j0], j1 = 0;
					double delta = 0;

					for (int j = 1; j < columns.Length; ++j)
					{
						if (!used[j])
						{
							var cur = weights[i0, j] - lines[i0] - columns[j];

							if (cur < minv[j])
							{
								minv[j] = cur;
								way[j] = j0;
							}

							if (minv[j] < delta)
							{
								delta = minv[j];
								j1 = j;
							}
						}
					}

					for (int j = 0; j < columns.Length; ++j)
					{
						if (used[j])
						{
							lines[matching[j]] += delta;
							columns[j] -= delta;
						}
						else
						{
							minv[j] -= delta;
						}
					}

					j0 = j1;
				}
				while (matching[j0] != 0);

				do
				{
					int j1 = way[j0];
					matching[j0] = matching[j1];
					j0 = j1;
				} while (j0 != 0);
			}

			var ans = new int[lines.Length];

			for (int j = 1; j < columns.Length; ++j)
			{
				ans[matching[j]] = j;
			}

			return ans;
		}
	}
}
