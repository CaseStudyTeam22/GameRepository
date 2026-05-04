using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;

namespace GamblingAction.Gameplay.SkillPreview
{
	public class ForwardOnePattern : ISkillPattern
	{
		public IEnumerable<(int x, int y)> ResolveCells(LocalIntent intent, PlayerDto me)
		{
			if (string.IsNullOrEmpty(intent.Dir)) yield break;
			int tx = me.X, ty = me.Y;
			switch (intent.Dir)
			{
				case "up":    ty -= 1; break;
				case "down":  ty += 1; break;
				case "left":  tx -= 1; break;
				case "right": tx += 1; break;
			}
			yield return (tx, ty);
		}
	}
}
