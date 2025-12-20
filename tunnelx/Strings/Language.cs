using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tunnelx.Strings
{
    public class Language
    {
        public enum language
        {
            PT = 1,
            EN = 2,
            ES = 3
        }
        private language _languageDefault;

        public Language(language languageDefault)
        {
            _languageDefault = languageDefault;
        }

        public string Text(string text)
        {
            var data = "";

            switch (text)
            {
                case "no-data":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Não há dados!"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "no-search":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Nenhum encontrado!"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "loading":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Carregando ..."; break;
                        case language.EN: data = "Loading ..."; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "no-signal":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Internet não identificada"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "signal":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Provável conexão de internet encontrada"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "select-interface":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Selecione a interface que será utilizada no túnel"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
                case "notice":
                    switch (_languageDefault)
                    {
                        case language.PT: data = "Aviso"; break;
                        case language.EN: data = ""; break;
                        case language.ES: data = ""; break;
                    }
                    break;
            }

            return data;
        }
    }
}
