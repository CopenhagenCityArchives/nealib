﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by xsd, Version=4.8.3928.0.
// 
namespace NEA.ArchiveModel.BKG1007 {
    using System.Xml.Serialization;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.sa.dk/xmlns/diark/1.0")]
    [System.Xml.Serialization.XmlRootAttribute("fileIndex", Namespace="http://www.sa.dk/xmlns/diark/1.0", IsNullable=false)]
    public partial class fileIndexType {
        
        private fileIndexTypeF[] fField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("f")]
        public fileIndexTypeF[] f {
            get {
                return this.fField;
            }
            set {
                this.fField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://www.sa.dk/xmlns/diark/1.0")]
    public partial class fileIndexTypeF {
        
        private string foNField;
        
        private string fiNField;
        
        private byte[] md5Field;
        
        /// <remarks/>
        public string foN {
            get {
                return this.foNField;
            }
            set {
                this.foNField = value;
            }
        }
        
        /// <remarks/>
        public string fiN {
            get {
                return this.fiNField;
            }
            set {
                this.fiNField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType="hexBinary")]
        public byte[] md5 {
            get {
                return this.md5Field;
            }
            set {
                this.md5Field = value;
            }
        }
    }
}
