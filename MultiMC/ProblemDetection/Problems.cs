// 
//  Copyright 2012  Andrew Okin
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using System;
using System.Collections;
using System.Collections.Generic;

namespace MultiMC.ProblemDetection
{
	public class Problems
	{
		static ArrayList problems = new ArrayList();
		
		/// <summary>
		/// Gets a problem relevant to the given line of output.
		/// </summary>
		/// <param name="mcOutput">
		/// Minecraft's error output
		/// </param>
		/// <returns>
		/// The relevant problem or null if there isn't one.
		/// </returns>
		public static IMinecraftProblem GetRelevantProblem(string mcOutput)
		{
			if (!Initialized)
				InitProblems();
			
			foreach (IMinecraftProblem prob in problems)
			{
				if (prob.IsRelevant(mcOutput))
				{
					return prob;
				}
			}
			return null;
		}
		
		public static void RegisterProblem(IMinecraftProblem prob)
		{
			if (problems.Count < 1)
			{
				problems.Add(prob);
				return;
			}
			for (int i = 0; i < problems.Count; i++)
			{
				if ((problems[i] as IMinecraftProblem) == null)
					continue;
				if (prob.GetPriority() > (problems[i] as IMinecraftProblem).GetPriority())
				{
					problems.Insert(i, prob);
					return;
				}
			}
		}
		
		public static void UnregisterProblem(IMinecraftProblem prob)
		{
			problems.Remove(prob);
		}
		
		/// <summary>
		/// Initializes the <see cref="MultiMC.ProblemDetection.Problems"/> class by
		/// registering problems.
		/// </summary>
		public static void InitProblems()
		{
			RegisterProblem(new BasicProblem(
				"MultiMC has detected an error. This might be because you are using the wrong " +
				"version of your mods. Try redownloading and then reinstalling them.",
				true, 0,
				"java.lang.VerifyError"));
			
			Initialized = true;
		}
		
		public static bool Initialized
		{
			get;
			private set;
		}
	}
}

