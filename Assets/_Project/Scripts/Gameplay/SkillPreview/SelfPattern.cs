using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;

namespace GamblingAction.Gameplay.SkillPreview
{
	public class SelfPattern : ISkillPattern
	{
		public IEnumerable<(int x, int y)> ResolveCells(LocalIntent intent, PlayerDto me)
		{
			yield return (me.X, me.Y);
		}
	}
}
