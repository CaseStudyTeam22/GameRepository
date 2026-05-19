// おそらくいらない


namespace GamblingAction.Core
{
    public class StaminaContainer : ModifierContainer
	{
		// スタミナ用の特殊な処理(これ最大スタミナだけだねターゲット)
		// そもそもdtoに書いてはいけない気がするので構造用検討

		public override void AddModifier(string tag, Modifier modifier)
		{
			base.AddModifier(tag, modifier);
			// 補正値をdtoのプロパティに保存->やっていいのかな

            // というかこれらの処理をGameState側でやらないとDto直接いじれない
            // とはいえここでoverrideしておかないと、他の場所で変えた時に不便になる

			// staminabar側に変更を通知し見た目を変える
		}

		public override void RemoveModifier(string tag)
		{
			base.RemoveModifier(tag);
			// 補正値をdtoのプロパティに保存

			// staminabar側に変更を通知し見た目を変える
		}
	}
}