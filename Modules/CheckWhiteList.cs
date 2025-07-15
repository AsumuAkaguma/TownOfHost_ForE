using System;
using System.IO;
using System.Reflection;

using ForhiteListEngine;

namespace TownOfHostForE.Modules
{
    internal class CheckWhiteList
    {
        public static bool CheckWhiteListData()
        {
            bool result = WhiteListDll.WhiteListEngine(EOSManager.Instance.friendCode);
            Logger.Info("WhiteList",$"ホワイトリストチェック結果：{result}");
            return result;
        }
    }
}
