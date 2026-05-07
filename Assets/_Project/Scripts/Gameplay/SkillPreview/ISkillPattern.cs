using System.Collections.Generic;
using GamblingAction.Core.Dto;
using GamblingAction.Domain;

namespace GamblingAction.Gameplay.SkillPreview
{
	/// <summary>
	/// </summary>
	public interface ISkillPattern
	{
		IEnumerable<(int x, int y)> ResolveCells(LocalIntent intent, PlayerDto me);
	}
}
