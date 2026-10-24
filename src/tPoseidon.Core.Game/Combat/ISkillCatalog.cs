using System.Collections.Generic;

namespace tPoseidon.Core.Game.Combat;

public interface ISkillCatalog
{
    ISkill? GetById(ushort skillId);
    IEnumerable<ISkill> All();
}
