using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;
using UnityEngine;

namespace GamblingAction.Gameplay.SkillPreview
{
	public class LineByPowerPattern : ISkillPattern
	{
		public IEnumerable<(int x, int y)> ResolveCells(LocalIntent intent, PlayerDto me)
		{
			if (string.IsNullOrEmpty(intent.Dir)) yield break;
			int dist = Mathf.Clamp(intent.Power, 1, 3);
			for (int i = 1; i <= dist; i++)
			{
				int tx = me.X, ty = me.Y;
				switch (intent.Dir)
				{
					case "up":    ty -= i; break;
					case "down":  ty += i; break;
					case "left":  tx -= i; break;
					case "right": tx += i; break;
				}
				yield return (tx, ty);
			}
		}
	}
}
