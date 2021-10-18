using System;
using System.Collections.Generic;

namespace KERBALISM
{
	public class CategorizedIOList : IDisposable
	{
		// note : this is a "leaking" pool, by design. Ensuring all created objets are returned would
		// be a nightmare, this object pool is only here to prevent the bulk of the allocations after 
		// the first few updates, but objects will leak on scene switches and in a bunch of other cases.
		private static readonly ObjectPool<CategorizedIOList> pool = new ObjectPool<CategorizedIOList>();

		public RecipeCategory category;
		public List<RecipeIO> recipes = new List<RecipeIO>();
		public double totalRate;

		public void Dispose()
		{
			category = null;
			recipes.Clear();
			totalRate = 0.0;
		}

		internal static void Categorize(List<RecipeIO> unsortedIOList, List<CategorizedIOList> categorizedList)
		{
			foreach (CategorizedIOList categorizedIoList in categorizedList)
				pool.Return(categorizedIoList);

			categorizedList.Clear();

			if (unsortedIOList.Count == 0)
				return;

			unsortedIOList.Sort((a, b) => a.recipe.category.CompareTo(b.recipe.category));

			CategorizedIOList currentCategory = pool.Get();
			RecipeIO firstIO = unsortedIOList[0];
			currentCategory.recipes.Add(firstIO);
			currentCategory.category = firstIO.recipe.category;
			currentCategory.totalRate += firstIO.SignedExecutedRate;

			for (int i = 1; i < unsortedIOList.Count; i++)
			{
				RecipeIO io = unsortedIOList[i];
				if (io.recipe.category != currentCategory.category)
				{
					categorizedList.Add(currentCategory);
					currentCategory = pool.Get();
					currentCategory.category = io.recipe.category;
				}

				currentCategory.recipes.Add(io);
				currentCategory.totalRate += io.SignedExecutedRate;
			}

			categorizedList.Add(currentCategory);
		}
	}
}
