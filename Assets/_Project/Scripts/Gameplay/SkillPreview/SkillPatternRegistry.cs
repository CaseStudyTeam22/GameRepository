using System.Collections.Generic;
using GamblingAction.Core.Skills;

namespace GamblingAction.Gameplay.SkillPreview
{
	/// <summary>
	/// </summary>
	public static class SkillPatternRegistry
	{
		static readonly Dictionary<SkillPatternType, ISkillPattern> _map = Build();

		static Dictionary<SkillPatternType, ISkillPattern> Build()
		{
			return new Dictionary<SkillPatternType, ISkillPattern>
			{
				{ SkillPatternType.LineByPower, new LineByPowerPattern() },
				{ SkillPatternType.Self,        new SelfPattern() },
				{ SkillPatternType.ForwardOne,  new ForwardOnePattern() },
			};
		}

		public static ISkillPattern Get(SkillPatternType type)
		{
			return _map.TryGetValue(type, out var p) ? p : null;
		}
	}
}
