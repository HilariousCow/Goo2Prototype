using System.Collections.Generic;

namespace Ludopathic.Goo.Data
{
    struct GooGroupData : ITeamID
    {
        public int TeamID { get; set; }
        public List<BlobData> ListOfBlobs;//compiled by a grouping job which does spatial searches for blobs of the same team id in the same radius.
    }

}