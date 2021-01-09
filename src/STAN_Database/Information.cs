using ProtoBuf;
using System.Collections.Generic;

namespace STAN_Database
{
    [ProtoContract(SkipConstructor = true)]
    public class Information
    {
        [ProtoMember(1)] private Dictionary<int, PartInfo> InfoPart { get; set; }

        public Information()
        {

        }

        public void ClearPartInfo()
        {
            InfoPart = new Dictionary<int, PartInfo>();
        }

        public void AddPart(int id)
        {
            InfoPart.Add(id, new PartInfo());
        }

        public PartInfo GetPart(int id)
        {
            return InfoPart[id];
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class PartInfo
    {
        [ProtoMember(1)] public int ColorID { get; set; }
        [ProtoMember(2)] public int MatID { get; set; }
        [ProtoMember(3)] public string Name { get; set; }
        [ProtoMember(4)] public string HEX_Type { get; set; }
        [ProtoMember(5)] public string PENTA_Type { get; set; }
        [ProtoMember(6)] public string TET_Type { get; set; }

        public PartInfo()
        {
            // Default data
            ColorID = 0;
            MatID = 0;
            Name = "blank";
            HEX_Type = "blank";
            PENTA_Type = "blank";
            TET_Type = "blank";
        }

        public void SetData(int colorid, int matid, string name, string[] FE_types)
        {
            ColorID = colorid;
            MatID = matid;
            Name = name;
            HEX_Type = FE_types[0];
            PENTA_Type = FE_types[1];
            TET_Type = FE_types[2];
        }
    }
    
}
