using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NEA.ArchiveModel.BKG1007
{
    public partial class tableType
    {
        public string GetTableRowsPath(string avFilePath) 
        {
            return $"{avFilePath}\\Tables\\{this.folder}\\{this.folder}.xml";
        }
    }
}
