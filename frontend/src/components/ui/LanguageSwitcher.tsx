import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';

interface LanguageSwitcherProps {
  className?: string;
}

export function LanguageSwitcher({ className = '' }: LanguageSwitcherProps) {
  const { i18n } = useTranslation();
  const currentLang = i18n.language;

  const toggleLanguage = () => {
    const newLang = currentLang === 'vi' ? 'en' : 'vi';
    i18n.changeLanguage(newLang);
  };

  return (
    <motion.button
      onClick={toggleLanguage}
      className={`
        flex items-center gap-2 px-3 py-2
        bg-white/80 backdrop-blur-sm
        rounded-full shadow-cute
        text-sm font-medium text-forest-dark
        hover:bg-white
        transition-colors
        ${className}
      `}
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.95 }}
    >
      <span className="text-lg">{currentLang === 'vi' ? '🇻🇳' : '🇬🇧'}</span>
      <span className="font-display">{currentLang === 'vi' ? 'VI' : 'EN'}</span>
    </motion.button>
  );
}

export default LanguageSwitcher;
