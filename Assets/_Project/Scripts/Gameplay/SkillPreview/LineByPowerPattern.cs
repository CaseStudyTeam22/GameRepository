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

			var state = GameStateLocator.Current;
			var opponent = state?.Opponent;

			bool isPush = intent.Mode == IntentTypes.Push;
			bool willPushOpponent = false;
			int knockbackDist = 0;
			int ox = -1, oy = -1;

			if (isPush && opponent != null)
			{
				int startDist = Mathf.Abs(opponent.X - me.X) + Mathf.Abs(opponent.Y - me.Y);
				if (startDist == 1)
				{
					bool match = false;
					if (intent.Dir == "up" && opponent.Y < me.Y) match = true;
					else if (intent.Dir == "down" && opponent.Y > me.Y) match = true;
					else if (intent.Dir == "left" && opponent.X < me.X) match = true;
					else if (intent.Dir == "right" && opponent.X > me.X) match = true;

					if (match)
					{
						willPushOpponent = true;
						ox = opponent.X;
						oy = opponent.Y;
						knockbackDist = Mathf.Max(1, 2 + Mathf.FloorToInt((10 - opponent.Stamina) / 2f));
					}
				}
			}

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

			if (willPushOpponent && knockbackDist > 0)
			{
				for (int i = 1; i <= knockbackDist; i++)
				{
					int tx = ox, ty = oy;
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
}
