using System.Collections.Generic;
using GamblingAction.Core.Skills;

namespace GamblingAction.Gameplay.SkillPreview
{
	/// <summary>
	/// </summary>
	public static class SkillPatternRegistry
	{
		private static readonly Dictionary<ESkillPatternType, ISkillPattern> s_Map = Build();

		private static Dictionary<ESkillPatternType, ISkillPattern> Build()
		{
			return new Dictionary<ESkillPatternType, ISkillPattern>
			{
				{ ESkillPatternType.LineByPower, new LineByPowerPattern() },
				{ ESkillPatternType.Self,        new SelfPattern() },
				{ ESkillPatternType.ForwardOne,  new ForwardOnePattern() },
			};
		}

		public static ISkillPattern Get(ESkillPatternType type)
		{
			return s_Map.TryGetValue(type, out var p) ? p : null;
		}
	}
}
